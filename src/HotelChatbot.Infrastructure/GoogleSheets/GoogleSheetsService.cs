using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using HotelChatbot.Core.Interfaces;
using HotelChatbot.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelChatbot.Infrastructure.GoogleSheets;

public class GoogleSheetsOptions
{
    public string SpreadsheetId { get; set; } = "";
    public string CredentialsPath { get; set; } = "";
    public string CredentialsJson { get; set; } = ""; // Alternative: inline JSON
    public int CacheMinutes { get; set; } = 5;
    // Multi-hotel mapping: hotelId → SpreadsheetId
    // Nếu để trống, mọi hotel đều dùng SpreadsheetId ở trên (single-hotel mode)
    public Dictionary<string, string> HotelSpreadsheetMapping { get; set; } = new();
}

// ============================================================
// SHEET NAME CONSTANTS
// ============================================================
public static class SheetNames
{
    public const string HotelInfo = "HotelInfo";
    public const string Rooms = "Rooms";
    public const string Pricing = "Pricing";
    public const string Availability = "Availability";
    public const string Amenities = "Amenities";
    public const string Promotions = "Promotions";
    public const string Bookings = "Bookings";
    public const string FAQs = "FAQs";
    public const string Holidays = "Holidays";
    public const string HotelConfig = "HotelConfig";
    public const string AiUsageLogs = "AiUsageLogs";
}

public class GoogleSheetsService : IGoogleSheetsService
{
    private readonly SheetsService _sheetsService;
    private readonly GoogleSheetsOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GoogleSheetsService> _logger;

    public GoogleSheetsService(
        IOptions<GoogleSheetsOptions> options,
        IMemoryCache cache,
        ILogger<GoogleSheetsService> logger)
    {
        _options = options.Value;
        _cache = cache;
        _logger = logger;
        _sheetsService = CreateSheetsService();
    }

    private SheetsService CreateSheetsService()
    {
        GoogleCredential credential;

        if (!string.IsNullOrEmpty(_options.CredentialsJson))
        {
            credential = GoogleCredential
                .FromJson(_options.CredentialsJson)
                .CreateScoped(SheetsService.Scope.Spreadsheets);
        }
        else
        {
            credential = GoogleCredential
                .FromFile(_options.CredentialsPath)
                .CreateScoped(SheetsService.Scope.Spreadsheets);
        }

        return new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "HotelChatbot"
        });
    }

    // ============================================================
    // MULTI-HOTEL ROUTING
    // ============================================================

    /// <summary>
    /// Resolve spreadsheetId cho từng hotel. Fallback về SpreadsheetId mặc định (platform).
    /// </summary>
    private string ResolveSpreadsheetId(string hotelId)
    {
        if (!string.IsNullOrEmpty(hotelId)
            && _options.HotelSpreadsheetMapping.TryGetValue(hotelId, out var id)
            && !string.IsNullOrEmpty(id))
        {
            return id;
        }
        return _options.SpreadsheetId;
    }

    /// <summary>
    /// Trả về tất cả spreadsheetId đang được cấu hình (dùng để tìm kiếm cross-hotel).
    /// </summary>
    private IEnumerable<string> GetAllSpreadsheetIds()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(_options.SpreadsheetId) && seen.Add(_options.SpreadsheetId))
            yield return _options.SpreadsheetId;

        foreach (var id in _options.HotelSpreadsheetMapping.Values)
        {
            if (!string.IsNullOrEmpty(id) && seen.Add(id))
                yield return id;
        }
    }

    // ============================================================
    // PRIVATE HELPERS
    // ============================================================

    private async Task<IList<IList<object>>> ReadSheetAsync(string spreadsheetId, string sheetName, string range = "A:Z")
    {
        try
        {
            var fullRange = $"{sheetName}!{range}";
            var request = _sheetsService.Spreadsheets.Values.Get(spreadsheetId, fullRange);
            var response = await request.ExecuteAsync();
            return response.Values ?? new List<IList<object>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading sheet {SheetName} from spreadsheet {SpreadsheetId}", sheetName, spreadsheetId);
            return new List<IList<object>>();
        }
    }

    private async Task EnsureSheetExistsAsync(string spreadsheetId, string sheetName)
    {
        var spreadsheet = await _sheetsService.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
        var exists = spreadsheet.Sheets?.Any(s =>
            string.Equals(s.Properties?.Title, sheetName, StringComparison.OrdinalIgnoreCase)) == true;

        if (exists) return;

        var addSheetRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request>
            {
                new()
                {
                    AddSheet = new AddSheetRequest
                    {
                        Properties = new SheetProperties
                        {
                            Title = sheetName
                        }
                    }
                }
            }
        };

        await _sheetsService.Spreadsheets.BatchUpdate(addSheetRequest, spreadsheetId).ExecuteAsync();
    }

    private async Task EnsureAiUsageSheetReadyAsync()
    {
        // AI usage logs ghi vào platform spreadsheet (SpreadsheetId mặc định)
        var spreadsheetId = _options.SpreadsheetId;
        await EnsureSheetExistsAsync(spreadsheetId, SheetNames.AiUsageLogs);
        var rows = await ReadSheetAsync(spreadsheetId, SheetNames.AiUsageLogs, "A:Q");
        if (rows.Count > 0) return;

        var header = new ValueRange
        {
            Values = new List<IList<object>>
            {
                new List<object>
                {
                    "LogId", "TimestampUtc", "HotelId", "SessionId", "Provider", "Model", "Operation",
                    "PromptTokens", "CompletionTokens", "TotalTokens", "SystemPromptChars",
                    "HistoryChars", "UserMessageChars", "InputTextChars", "OutputChars",
                    "EstimatedCostUsd", "EstimatedCostVnd"
                }
            }
        };

        var updateRequest = _sheetsService.Spreadsheets.Values.Update(header, spreadsheetId, $"{SheetNames.AiUsageLogs}!A1:Q1");
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await updateRequest.ExecuteAsync();
    }

    private string GetCellValue(IList<object> row, int index, string defaultValue = "")
    {
        if (index < row.Count && row[index] != null)
            return row[index].ToString()?.Trim() ?? defaultValue;
        return defaultValue;
    }

    private decimal GetDecimalValue(IList<object> row, int index)
    {
        var val = GetCellValue(row, index, "0");
        return decimal.TryParse(val.Replace(",", ""), out var result) ? result : 0;
    }

    private int GetIntValue(IList<object> row, int index)
    {
        var val = GetCellValue(row, index, "0");
        return int.TryParse(val, out var result) ? result : 0;
    }

    private double GetDoubleValue(IList<object> row, int index)
    {
        var val = GetCellValue(row, index, "0");
        return double.TryParse(val, out var result) ? result : 0;
    }

    private bool GetBoolValue(IList<object> row, int index)
    {
        var val = GetCellValue(row, index, "false").ToLower();
        return val == "true" || val == "yes" || val == "1" || val == "có";
    }

    private DateTime GetDateValue(IList<object> row, int index)
    {
        var val = GetCellValue(row, index);
        return DateTime.TryParse(val, out var result) ? result : DateTime.MinValue;
    }

    // ============================================================
    // READ: HOTEL INFO
    // ============================================================
    public async Task<HotelInfo?> GetHotelInfoAsync(string hotelId)
    {
        var cacheKey = $"hotel_info_{hotelId}";
        if (_cache.TryGetValue(cacheKey, out HotelInfo? cached)) return cached;

        var spreadsheetId = ResolveSpreadsheetId(hotelId);
        var rows = await ReadSheetAsync(spreadsheetId, SheetNames.HotelInfo);
        if (rows.Count < 2) return null;

        var hotelRow = rows.Skip(1).FirstOrDefault(r => GetCellValue(r, 0) == hotelId || hotelId == "default");
        if (hotelRow == null) hotelRow = rows.Count > 1 ? rows[1] : null;
        if (hotelRow == null) return null;

        var info = new HotelInfo
        {
            HotelId = GetCellValue(hotelRow, 0),
            Name = GetCellValue(hotelRow, 1),
            Description = GetCellValue(hotelRow, 2),
            Address = GetCellValue(hotelRow, 3),
            LocationDescription = GetCellValue(hotelRow, 4),
            PhoneNumber = GetCellValue(hotelRow, 5),
            Email = GetCellValue(hotelRow, 6),
            Website = GetCellValue(hotelRow, 7),
            NearbyLandmarks = GetCellValue(hotelRow, 8),
            StarRating = GetCellValue(hotelRow, 9),
            CheckInTime = GetCellValue(hotelRow, 10, "14:00"),
            CheckOutTime = GetCellValue(hotelRow, 11, "12:00"),
            EarlyCheckInPolicy = GetCellValue(hotelRow, 12),
            LateCheckOutPolicy = GetCellValue(hotelRow, 13),
            CancellationPolicy = GetCellValue(hotelRow, 14),
            PetPolicy = GetCellValue(hotelRow, 15),
            SmokingPolicy = GetCellValue(hotelRow, 16),
            ChildPolicy = GetCellValue(hotelRow, 17),
            PaymentMethods = GetCellValue(hotelRow, 18),
            Languages = GetCellValue(hotelRow, 19),
            SocialMedia = GetCellValue(hotelRow, 20)
        };

        _cache.Set(cacheKey, info, TimeSpan.FromMinutes(_options.CacheMinutes));
        return info;
    }

    // ============================================================
    // READ: ROOMS
    // ============================================================
    public async Task<List<Room>> GetRoomsAsync(string hotelId)
    {
        var cacheKey = $"rooms_{hotelId}";
        if (_cache.TryGetValue(cacheKey, out List<Room>? cached)) return cached!;

        var spreadsheetId = ResolveSpreadsheetId(hotelId);
        var rows = await ReadSheetAsync(spreadsheetId, SheetNames.Rooms);
        var rooms = new List<Room>();

        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 2) continue;
            var hotelIdCell = GetCellValue(row, 0);
            if (hotelId != "default" && !string.IsNullOrEmpty(hotelIdCell) && hotelIdCell != hotelId) continue;

            rooms.Add(new Room
            {
                HotelId = hotelIdCell,
                RoomId = GetCellValue(row, 1),
                RoomNumber = GetCellValue(row, 2),
                RoomType = GetCellValue(row, 3),
                RoomTypeName = GetCellValue(row, 4),
                Floor = GetIntValue(row, 5),
                SizeM2 = GetDoubleValue(row, 6),
                MaxAdults = GetIntValue(row, 7),
                MaxChildren = GetIntValue(row, 8),
                MaxOccupancy = GetIntValue(row, 9),
                BedType = GetCellValue(row, 10),
                View = GetCellValue(row, 11),
                Description = GetCellValue(row, 12),
                ShortDescription = GetCellValue(row, 13),
                Amenities = GetCellValue(row, 14),
                ImageUrls = GetCellValue(row, 15),
                Tags = GetCellValue(row, 16),
                IsActive = GetBoolValue(row, 17)
            });
        }

        _cache.Set(cacheKey, rooms, TimeSpan.FromMinutes(_options.CacheMinutes));
        return rooms;
    }

    // ============================================================
    // READ: PRICING
    // ============================================================
    public async Task<List<RoomPricing>> GetPricingAsync(string hotelId)
    {
        var cacheKey = $"pricing_{hotelId}";
        if (_cache.TryGetValue(cacheKey, out List<RoomPricing>? cached)) return cached!;

        var spreadsheetId = ResolveSpreadsheetId(hotelId);
        var rows = await ReadSheetAsync(spreadsheetId, SheetNames.Pricing);
        var pricingList = new List<RoomPricing>();

        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 3) continue;
            var hotelIdCell = GetCellValue(row, 0);
            if (hotelId != "default" && !string.IsNullOrEmpty(hotelIdCell) && hotelIdCell != hotelId) continue;

            pricingList.Add(new RoomPricing
            {
                HotelId = hotelIdCell,
                PricingId = GetCellValue(row, 1),
                RoomType = GetCellValue(row, 2),
                WeekdayPrice = GetDecimalValue(row, 3),
                WeekendPrice = GetDecimalValue(row, 4),
                HolidayPrice = GetDecimalValue(row, 5),
                PeakSeasonPrice = GetDecimalValue(row, 6),
                Currency = GetCellValue(row, 7, "VND"),
                ExtraAdultFee = GetDecimalValue(row, 8),
                ExtraChildFee = GetDecimalValue(row, 9),
                BreakfastIncluded = GetCellValue(row, 10, "No"),
                BreakfastFee = GetDecimalValue(row, 11),
                Notes = GetCellValue(row, 12),
                ValidFrom = GetDateValue(row, 13),
                ValidTo = GetDateValue(row, 14)
            });
        }

        _cache.Set(cacheKey, pricingList, TimeSpan.FromMinutes(_options.CacheMinutes));
        return pricingList;
    }

    // ============================================================
    // READ: AVAILABILITY
    // ============================================================
    public async Task<List<RoomAvailability>> GetAvailabilityAsync(string hotelId, DateTime checkIn, DateTime checkOut)
    {
        var spreadsheetId = ResolveSpreadsheetId(hotelId);
        var rows = await ReadSheetAsync(spreadsheetId, SheetNames.Availability);
        var availability = new List<RoomAvailability>();

        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 4) continue;
            var hotelIdCell = GetCellValue(row, 0);
            if (hotelId != "default" && !string.IsNullOrEmpty(hotelIdCell) && hotelIdCell != hotelId) continue;

            var date = GetDateValue(row, 4);
            if (date < checkIn || date >= checkOut) continue;

            availability.Add(new RoomAvailability
            {
                HotelId = hotelIdCell,
                AvailabilityId = GetCellValue(row, 1),
                RoomId = GetCellValue(row, 2),
                RoomNumber = GetCellValue(row, 3),
                RoomType = GetCellValue(row, 4) != "" ? GetCellValue(row, 4) : "",
                Date = date,
                Status = GetCellValue(row, 5, "Available"),
                BookingId = GetCellValue(row, 6),
                Notes = GetCellValue(row, 7)
            });
        }

        return availability;
    }

    // ============================================================
    // READ: AMENITIES
    // ============================================================
    public async Task<List<Amenity>> GetAmenitiesAsync(string hotelId)
    {
        var cacheKey = $"amenities_{hotelId}";
        if (_cache.TryGetValue(cacheKey, out List<Amenity>? cached)) return cached!;

        var spreadsheetId = ResolveSpreadsheetId(hotelId);
        var rows = await ReadSheetAsync(spreadsheetId, SheetNames.Amenities);
        var amenities = new List<Amenity>();

        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 2) continue;
            amenities.Add(new Amenity
            {
                AmenityId = GetCellValue(row, 0),
                Name = GetCellValue(row, 1),
                VietnameseName = GetCellValue(row, 2),
                Category = GetCellValue(row, 3),
                Description = GetCellValue(row, 4),
                Icon = GetCellValue(row, 5),
                IsComplimentary = GetBoolValue(row, 6),
                Fee = row.Count > 7 ? GetDecimalValue(row, 7) : null,
                OperatingHours = GetCellValue(row, 8),
                IsActive = GetBoolValue(row, 9)
            });
        }

        _cache.Set(cacheKey, amenities, TimeSpan.FromMinutes(_options.CacheMinutes));
        return amenities;
    }

    // ============================================================
    // READ: PROMOTIONS
    // ============================================================
    public async Task<List<Promotion>> GetActivePromotionsAsync(string hotelId)
    {
        var cacheKey = $"promos_{hotelId}";
        if (_cache.TryGetValue(cacheKey, out List<Promotion>? cached)) return cached!;

        var spreadsheetId = ResolveSpreadsheetId(hotelId);
        var rows = await ReadSheetAsync(spreadsheetId, SheetNames.Promotions);
        var promos = new List<Promotion>();
        var today = DateTime.Today;

        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 5) continue;
            var hotelIdCell = GetCellValue(row, 0);
            if (hotelId != "default" && !string.IsNullOrEmpty(hotelIdCell) && hotelIdCell != hotelId) continue;

            var validFrom = GetDateValue(row, 9);
            var validTo = GetDateValue(row, 10);
            var isActive = GetBoolValue(row, 14);

            if (!isActive || (validTo != DateTime.MinValue && validTo < today)) continue;

            promos.Add(new Promotion
            {
                HotelId = hotelIdCell,
                PromoId = GetCellValue(row, 1),
                PromoCode = GetCellValue(row, 2),
                Title = GetCellValue(row, 3),
                Description = GetCellValue(row, 4),
                DiscountType = GetCellValue(row, 5),
                DiscountValue = GetDecimalValue(row, 6),
                MinimumStayNights = GetDecimalValue(row, 7),
                MinimumSpend = GetDecimalValue(row, 8),
                ApplicableRoomTypes = GetCellValue(row, 9),
                ValidFrom = validFrom,
                ValidTo = validTo,
                BookingWindow = GetCellValue(row, 12),
                Conditions = GetCellValue(row, 13),
                IsActive = isActive,
                UsageLimit = GetIntValue(row, 15),
                UsageCount = GetIntValue(row, 16)
            });
        }

        _cache.Set(cacheKey, promos, TimeSpan.FromMinutes(_options.CacheMinutes));
        return promos;
    }

    // ============================================================
    // READ: FAQs
    // ============================================================
    public async Task<List<FAQ>> GetFAQsAsync(string hotelId)
    {
        var cacheKey = $"faqs_{hotelId}";
        if (_cache.TryGetValue(cacheKey, out List<FAQ>? cached)) return cached!;

        var spreadsheetId = ResolveSpreadsheetId(hotelId);
        var rows = await ReadSheetAsync(spreadsheetId, SheetNames.FAQs);
        var faqs = new List<FAQ>();

        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 3) continue;
            faqs.Add(new FAQ
            {
                FaqId = GetCellValue(row, 0),
                Category = GetCellValue(row, 1),
                Question = GetCellValue(row, 2),
                Answer = GetCellValue(row, 3),
                Keywords = GetCellValue(row, 4),
                Priority = GetIntValue(row, 5),
                IsActive = GetBoolValue(row, 6)
            });
        }

        _cache.Set(cacheKey, faqs, TimeSpan.FromMinutes(_options.CacheMinutes));
        return faqs;
    }

    // ============================================================
    // READ: HOLIDAYS
    // ============================================================
    public async Task<List<Holiday>> GetHolidaysAsync(string hotelId)
    {
        var cacheKey = $"holidays_{hotelId}";
        if (_cache.TryGetValue(cacheKey, out List<Holiday>? cached)) return cached!;

        var spreadsheetId = ResolveSpreadsheetId(hotelId);
        var rows = await ReadSheetAsync(spreadsheetId, SheetNames.Holidays);
        var holidays = new List<Holiday>();

        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 2) continue;
            holidays.Add(new Holiday
            {
                HolidayId = GetCellValue(row, 0),
                Name = GetCellValue(row, 1),
                Date = GetDateValue(row, 2),
                PriceType = GetCellValue(row, 3, "Holiday"),
                Notes = GetCellValue(row, 4)
            });
        }

        _cache.Set(cacheKey, holidays, TimeSpan.FromMinutes(_options.CacheMinutes));
        return holidays;
    }

    // ============================================================
    // WRITE: AI USAGE LOG (platform spreadsheet)
    // ============================================================
    public async Task AppendAiUsageLogAsync(AiUsageLog log)
    {
        try
        {
            await EnsureAiUsageSheetReadyAsync();
            var values = new List<IList<object>>
            {
                new List<object>
                {
                    log.LogId,
                    log.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    log.HotelId,
                    log.SessionId,
                    log.Provider,
                    log.Model,
                    log.Operation,
                    log.PromptTokens,
                    log.CompletionTokens,
                    log.TotalTokens,
                    log.SystemPromptChars,
                    log.HistoryChars,
                    log.UserMessageChars,
                    log.InputTextChars,
                    log.OutputChars,
                    log.EstimatedCostUsd,
                    log.EstimatedCostVnd
                }
            };

            var body = new ValueRange { Values = values };
            var request = _sheetsService.Spreadsheets.Values.Append(body, _options.SpreadsheetId, $"{SheetNames.AiUsageLogs}!A:Q");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending AI usage log {LogId}", log.LogId);
        }
    }

    // ============================================================
    // READ: AI USAGE LOGS (platform spreadsheet)
    // ============================================================
    public async Task<List<AiUsageLog>> GetAiUsageLogsAsync(DateTime fromUtc, DateTime toUtc, string? hotelId = null)
    {
        await EnsureAiUsageSheetReadyAsync();
        var rows = await ReadSheetAsync(_options.SpreadsheetId, SheetNames.AiUsageLogs, "A:Q");
        var logs = new List<AiUsageLog>();

        foreach (var row in rows.Skip(1))
        {
            if (row.Count < 7) continue;

            var log = new AiUsageLog
            {
                LogId = GetCellValue(row, 0),
                TimestampUtc = GetDateValue(row, 1),
                HotelId = GetCellValue(row, 2),
                SessionId = GetCellValue(row, 3),
                Provider = GetCellValue(row, 4),
                Model = GetCellValue(row, 5),
                Operation = GetCellValue(row, 6),
                PromptTokens = GetIntValue(row, 7),
                CompletionTokens = GetIntValue(row, 8),
                TotalTokens = GetIntValue(row, 9),
                SystemPromptChars = GetIntValue(row, 10),
                HistoryChars = GetIntValue(row, 11),
                UserMessageChars = GetIntValue(row, 12),
                InputTextChars = GetIntValue(row, 13),
                OutputChars = GetIntValue(row, 14),
                EstimatedCostUsd = GetDecimalValue(row, 15),
                EstimatedCostVnd = GetDecimalValue(row, 16)
            };

            if (log.TimestampUtc < fromUtc || log.TimestampUtc > toUtc) continue;
            if (!string.IsNullOrWhiteSpace(hotelId) && !string.Equals(log.HotelId, hotelId, StringComparison.OrdinalIgnoreCase)) continue;

            logs.Add(log);
        }

        return logs;
    }

    // ============================================================
    // WRITE: CREATE BOOKING (hotel spreadsheet)
    // ============================================================
    public async Task<string> CreateBookingAsync(Booking booking)
    {
        try
        {
            var spreadsheetId = ResolveSpreadsheetId(booking.HotelId);
            var values = new List<IList<object>>
            {
                new List<object>
                {
                    booking.HotelId,
                    booking.BookingId,
                    booking.GuestName,
                    booking.GuestPhone,
                    booking.GuestEmail,
                    booking.GuestIdCard,
                    booking.Nationality,
                    booking.RoomId,
                    booking.RoomNumber,
                    booking.RoomType,
                    booking.CheckInDate.ToString("yyyy-MM-dd"),
                    booking.CheckOutDate.ToString("yyyy-MM-dd"),
                    booking.TotalNights,
                    booking.NumAdults,
                    booking.NumChildren,
                    booking.SpecialRequests,
                    booking.RoomRate,
                    booking.TotalAmount,
                    booking.PromoCode,
                    booking.DiscountAmount,
                    booking.FinalAmount,
                    booking.BreakfastIncluded ? "Yes" : "No",
                    booking.PaymentMethod,
                    booking.PaymentStatus,
                    booking.BookingStatus,
                    booking.BookingChannel,
                    booking.SessionId,
                    booking.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    booking.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    booking.Notes
                }
            };

            var body = new ValueRange { Values = values };
            var request = _sheetsService.Spreadsheets.Values.Append(body, spreadsheetId, $"{SheetNames.Bookings}!A:AD");
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();

            _logger.LogInformation("Booking {BookingId} created in spreadsheet {SpreadsheetId}", booking.BookingId, spreadsheetId);
            return booking.BookingId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking {BookingId}", booking.BookingId);
            throw;
        }
    }

    // ============================================================
    // WRITE: UPDATE AVAILABILITY (hotel spreadsheet)
    // ============================================================
    public async Task<bool> UpdateAvailabilityAsync(string hotelId, string roomId, DateTime checkIn, DateTime checkOut, string bookingId)
    {
        try
        {
            var spreadsheetId = ResolveSpreadsheetId(hotelId);
            var rows = await ReadSheetAsync(spreadsheetId, SheetNames.Availability);
            var updates = new List<ValueRange>();
            var rowsToAdd = new List<IList<object>>();
            var existingDates = new HashSet<string>();

            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowRoomId = GetCellValue(row, 2);
                var rowDate = GetDateValue(row, 4);

                if (rowRoomId == roomId && rowDate >= checkIn && rowDate < checkOut)
                {
                    existingDates.Add(rowDate.ToString("yyyy-MM-dd"));
                    var range = $"{SheetNames.Availability}!F{i + 1}:G{i + 1}";
                    updates.Add(new ValueRange
                    {
                        Range = range,
                        Values = new List<IList<object>> { new List<object> { "Booked", bookingId } }
                    });
                }
            }

            // Add missing date rows
            for (var date = checkIn; date < checkOut; date = date.AddDays(1))
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                if (!existingDates.Contains(dateStr))
                {
                    rowsToAdd.Add(new List<object>
                    {
                        hotelId, Guid.NewGuid().ToString("N")[..8], roomId, "", dateStr, "Booked", bookingId, ""
                    });
                }
            }

            if (updates.Count > 0)
            {
                var batchRequest = new BatchUpdateValuesRequest
                {
                    Data = updates,
                    ValueInputOption = "USER_ENTERED"
                };
                await _sheetsService.Spreadsheets.Values.BatchUpdate(batchRequest, spreadsheetId).ExecuteAsync();
            }

            if (rowsToAdd.Count > 0)
            {
                var body = new ValueRange { Values = rowsToAdd };
                var appendReq = _sheetsService.Spreadsheets.Values.Append(body, spreadsheetId, $"{SheetNames.Availability}!A:H");
                appendReq.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                await appendReq.ExecuteAsync();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating availability for room {RoomId}", roomId);
            return false;
        }
    }

    // ============================================================
    // READ: GET BOOKING BY ID (tìm qua tất cả hotel spreadsheets)
    // ============================================================
    public async Task<Booking?> GetBookingAsync(string bookingId)
    {
        foreach (var spreadsheetId in GetAllSpreadsheetIds())
        {
            var rows = await ReadSheetAsync(spreadsheetId, SheetNames.Bookings);
            var row = rows.Skip(1).FirstOrDefault(r => GetCellValue(r, 1) == bookingId);
            if (row == null) continue;

            return new Booking
            {
                HotelId = GetCellValue(row, 0),
                BookingId = GetCellValue(row, 1),
                GuestName = GetCellValue(row, 2),
                GuestPhone = GetCellValue(row, 3),
                GuestEmail = GetCellValue(row, 4),
                GuestIdCard = GetCellValue(row, 5),
                Nationality = GetCellValue(row, 6),
                RoomId = GetCellValue(row, 7),
                RoomNumber = GetCellValue(row, 8),
                RoomType = GetCellValue(row, 9),
                CheckInDate = GetDateValue(row, 10),
                CheckOutDate = GetDateValue(row, 11),
                TotalNights = GetIntValue(row, 12),
                NumAdults = GetIntValue(row, 13),
                NumChildren = GetIntValue(row, 14),
                SpecialRequests = GetCellValue(row, 15),
                RoomRate = GetDecimalValue(row, 16),
                TotalAmount = GetDecimalValue(row, 17),
                PromoCode = GetCellValue(row, 18),
                DiscountAmount = GetDecimalValue(row, 19),
                FinalAmount = GetDecimalValue(row, 20),
                BreakfastIncluded = GetCellValue(row, 21).ToLower() == "yes",
                PaymentMethod = GetCellValue(row, 22),
                PaymentStatus = GetCellValue(row, 23),
                BookingStatus = GetCellValue(row, 24),
                BookingChannel = GetCellValue(row, 25),
                SessionId = GetCellValue(row, 26),
                CreatedAt = GetDateValue(row, 27),
                UpdatedAt = GetDateValue(row, 28),
                Notes = GetCellValue(row, 29)
            };
        }

        return null;
    }

    // ============================================================
    // WRITE: CANCEL BOOKING (tìm qua tất cả hotel spreadsheets)
    // ============================================================
    public async Task<bool> CancelBookingAsync(string bookingId, string reason)
    {
        foreach (var spreadsheetId in GetAllSpreadsheetIds())
        {
            try
            {
                var rows = await ReadSheetAsync(spreadsheetId, SheetNames.Bookings);
                for (int i = 1; i < rows.Count; i++)
                {
                    if (GetCellValue(rows[i], 1) != bookingId) continue;

                    var range = $"{SheetNames.Bookings}!Y{i + 1}:AD{i + 1}";
                    var values = new ValueRange
                    {
                        Values = new List<IList<object>>
                        {
                            new List<object> { "Cancelled", "", "", "", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), reason }
                        }
                    };
                    await _sheetsService.Spreadsheets.Values.Update(values, spreadsheetId, range).ExecuteAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching spreadsheet {SpreadsheetId} for booking {BookingId}", spreadsheetId, bookingId);
            }
        }

        _logger.LogWarning("Booking {BookingId} not found in any configured spreadsheet", bookingId);
        return false;
    }
}
