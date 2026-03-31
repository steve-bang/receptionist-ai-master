using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace HotelChatbot.Infrastructure.Services;

public class ConversationService : IConversationService
{
    private readonly IHotelAIService _ai;
    private readonly IRagContextService _ragContext;
    private readonly IHotelDataService _hotelData;
    private readonly IBookingService _bookingService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ConversationService> _logger;

    private const int MAX_HISTORY = 20; // Keep last 20 messages
    private const int SESSION_TIMEOUT_MINUTES = 30;

    public ConversationService(
        IHotelAIService ai,
        IRagContextService ragContext,
        IHotelDataService hotelData,
        IBookingService bookingService,
        IMemoryCache cache,
        ILogger<ConversationService> logger)
    {
        _ai = ai;
        _ragContext = ragContext;
        _hotelData = hotelData;
        _bookingService = bookingService;
        _cache = cache;
        _logger = logger;
    }

    public ConversationSession GetOrCreateSession(string sessionId, string hotelId)
    {
        var cacheKey = $"session_{sessionId}";
        if (_cache.TryGetValue(cacheKey, out ConversationSession? session) && session != null)
        {
            session.LastActivityAt = DateTime.UtcNow;
            return session;
        }

        session = new ConversationSession
        {
            SessionId = sessionId,
            HotelId = hotelId,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        _cache.Set(cacheKey, session, TimeSpan.FromMinutes(SESSION_TIMEOUT_MINUTES));
        return session;
    }

    public void UpdateSession(ConversationSession session)
    {
        var cacheKey = $"session_{session.SessionId}";
        session.LastActivityAt = DateTime.UtcNow;
        _cache.Set(cacheKey, session, TimeSpan.FromMinutes(SESSION_TIMEOUT_MINUTES));
    }

    public void AddMessageToSession(string sessionId, string role, string content)
    {
        var cacheKey = $"session_{sessionId}";
        if (_cache.TryGetValue(cacheKey, out ConversationSession? session) && session != null)
        {
            session.Messages.Add(new ConversationMessage { Role = role, Content = content });

            // Keep only recent messages to avoid token overflow
            if (session.Messages.Count > MAX_HISTORY)
                session.Messages = session.Messages.TakeLast(MAX_HISTORY).ToList();

            UpdateSession(session);
        }
    }

    // ============================================================
    // MAIN: PROCESS MESSAGE
    // ============================================================
    public async Task<ChatResponse> ProcessMessageAsync(ChatRequest request)
    {
        var sessionId = string.IsNullOrEmpty(request.SessionId)
            ? Guid.NewGuid().ToString()
            : request.SessionId;

        var session = GetOrCreateSession(sessionId, request.HotelId);

        // Analyze intent
        var intent = await _ai.AnalyzeIntentAsync(request.Message, session);
        ApplyDeterministicRelativeDateOverrides(request.Message, intent.ExtractedEntities);

        // Update booking draft with extracted entities
        UpdateBookingDraftFromEntities(session, intent.ExtractedEntities);

        // Build system prompt with fresh hotel data
        var systemPrompt = await _ai.BuildSystemPromptAsync(request.HotelId);

        var ragKnowledgeContext = await _ragContext.BuildKnowledgeContextAsync(
            request.HotelId,
            request.Message,
            intent.Intent,
            sessionId);
        if (!string.IsNullOrWhiteSpace(ragKnowledgeContext))
            systemPrompt += $"\n\n=== TRI THỨC LIÊN QUAN TỪ KNOWLEDGE BASE ===\n{ragKnowledgeContext}";

        // Add availability context if needed
        if (intent.Intent is "availability_check" or "booking_init" or "price_inquiry")
        {
            var availabilityContext = await BuildAvailabilityContextAsync(session, intent);
            if (!string.IsNullOrEmpty(availabilityContext))
                systemPrompt += $"\n\n=== THÔNG TIN PHÒNG TRỐNG HIỆN TẠI ===\n{availabilityContext}";
        }

        // Get AI response
        var aiResponse = await _ai.GetChatCompletionAsync(
            systemPrompt,
            session.Messages,
            request.Message,
            "chat_reply",
            request.HotelId,
            sessionId);

        // Check if booking is ready to be created
        var isBookingReady = aiResponse.Contains("[BOOKING_READY]");
        var cleanResponse = aiResponse.Replace("[BOOKING_READY]", "").Trim();

        BookingConfirmationDto? confirmation = null;
        string? bookingId = null;
        var shouldAttemptBooking = isBookingReady || intent.Intent == "booking_confirm";

        if (shouldAttemptBooking)
        {
            await TryResolveRoomIdAsync(session, request.HotelId);

            if (IsBookingDraftComplete(session.BookingDraft))
            {
                try
                {
                    var bookingRequest = BuildBookingRequest(session, request.HotelId);
                    confirmation = await _bookingService.CreateBookingAsync(bookingRequest);
                    bookingId = confirmation.BookingId;
                    cleanResponse = confirmation.ConfirmationMessage;
                    isBookingReady = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating booking for session {SessionId}", sessionId);
                    cleanResponse = $"Dạ, em xin lỗi, có lỗi xảy ra khi đặt phòng: {ex.Message}. Anh/chị vui lòng thử lại hoặc liên hệ trực tiếp với khách sạn nhé! 🙏";
                    isBookingReady = false;
                }
            }
            else if (intent.Intent == "booking_confirm")
            {
                cleanResponse = BuildBookingPendingMessage(session.BookingDraft);
                isBookingReady = false;
            }
        }

        // Save messages to session
        session.Messages.Add(new ConversationMessage { Role = "user", Content = request.Message });
        session.Messages.Add(new ConversationMessage { Role = "assistant", Content = cleanResponse });
        session.CurrentIntent = intent.Intent;
        UpdateSession(session);

        // Build room suggestions if applicable
        List<RoomSuggestionDto>? suggestions = null;
        if (intent.Intent is "availability_check" or "price_inquiry" or "booking_init")
        {
            suggestions = await BuildRoomSuggestionsAsync(session, intent, request.HotelId);
        }

        return new ChatResponse
        {
            SessionId = sessionId,
            Message = cleanResponse,
            Intent = intent.Intent,
            BookingDraft = session.BookingDraft,
            RoomSuggestions = suggestions,
            IsBookingComplete = isBookingReady && bookingId != null,
            BookingId = bookingId,
            Timestamp = DateTime.UtcNow
        };
    }

    // ============================================================
    // HELPERS
    // ============================================================
    private void UpdateBookingDraftFromEntities(ConversationSession session, Dictionary<string, string> entities)
    {
        var draft = session.BookingDraft;

        if (entities.TryGetValue("check_in_date", out var checkIn) && DateTime.TryParse(checkIn, out var checkInDate))
            draft.CheckInDate = checkInDate;

        if (entities.TryGetValue("check_out_date", out var checkOut) && DateTime.TryParse(checkOut, out var checkOutDate))
            draft.CheckOutDate = checkOutDate;

        if (entities.TryGetValue("num_adults", out var adults) && int.TryParse(adults, out var numAdults))
            draft.NumAdults = numAdults;

        if (entities.TryGetValue("num_children", out var children) && int.TryParse(children, out var numChildren))
            draft.NumChildren = numChildren;

        if (entities.TryGetValue("room_type", out var roomType) && !string.IsNullOrEmpty(roomType))
            draft.RoomType = roomType;

        if (entities.TryGetValue("guest_name", out var name) && !string.IsNullOrEmpty(name))
            draft.GuestName = name;

        if (entities.TryGetValue("guest_phone", out var phone) && !string.IsNullOrEmpty(phone))
            draft.GuestPhone = phone;

        if (entities.TryGetValue("promo_code", out var promo) && !string.IsNullOrEmpty(promo))
            draft.PromoCode = promo;
    }

    private void ApplyDeterministicRelativeDateOverrides(string message, Dictionary<string, string> entities)
    {
        var detectedDates = ExtractRelativeDates(message);
        if (detectedDates.Count == 0) return;

        if (detectedDates.Count >= 1)
            entities["check_in_date"] = detectedDates[0].ToString("yyyy-MM-dd");

        if (detectedDates.Count >= 2)
            entities["check_out_date"] = detectedDates[1].ToString("yyyy-MM-dd");
    }

    private static List<DateTime> ExtractRelativeDates(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        var today = GetVietnamToday();
        var results = new List<DateTime>();

        var weekdayMatches = Regex.Matches(normalized, @"(thứ\s*[2-7]|chủ nhật|cn)\s+tuần\s+(tới|sau|này)");
        foreach (Match match in weekdayMatches)
        {
            var target = ResolveWeekdayRelativeDate(match.Groups[1].Value, match.Groups[2].Value, today);
            if (target.HasValue)
                results.Add(target.Value);
        }

        var dayMonthMatches = Regex.Matches(normalized, @"ngày\s+(\d{1,2})\s+tháng\s+(tới|sau|này)");
        foreach (Match match in dayMonthMatches)
        {
            var day = int.Parse(match.Groups[1].Value);
            var target = ResolveDayOfMonthRelativeDate(day, match.Groups[2].Value, today);
            if (target.HasValue)
                results.Add(target.Value);
        }

        var ordered = new List<DateTime>();
        foreach (var date in results.Where(x => x.Date >= today))
        {
            if (!ordered.Contains(date))
                ordered.Add(date);
        }

        return ordered;
    }

    private static DateTime? ResolveWeekdayRelativeDate(string weekdayText, string relativeText, DateTime today)
    {
        var dayOfWeek = weekdayText switch
        {
            "thứ 2" => DayOfWeek.Monday,
            "thứ 3" => DayOfWeek.Tuesday,
            "thứ 4" => DayOfWeek.Wednesday,
            "thứ 5" => DayOfWeek.Thursday,
            "thứ 6" => DayOfWeek.Friday,
            "thứ 7" => DayOfWeek.Saturday,
            "chủ nhật" => DayOfWeek.Sunday,
            "cn" => DayOfWeek.Sunday,
            _ => (DayOfWeek?)null
        };

        if (!dayOfWeek.HasValue) return null;

        var currentWeekMonday = today.AddDays(-((7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7));
        var weekOffset = relativeText switch
        {
            "này" => 0,
            "tới" => 1,
            "sau" => 1,
            _ => 0
        };

        var targetWeekMonday = currentWeekMonday.AddDays(weekOffset * 7);
        var targetDate = targetWeekMonday.AddDays(((7 + (int)dayOfWeek.Value - (int)DayOfWeek.Monday) % 7));

        if (relativeText == "này" && targetDate.Date < today)
            targetDate = targetDate.AddDays(7);

        return targetDate.Date;
    }

    private static DateTime? ResolveDayOfMonthRelativeDate(int day, string relativeText, DateTime today)
    {
        if (day < 1 || day > 31) return null;

        var monthOffset = relativeText switch
        {
            "này" => 0,
            "tới" => 1,
            "sau" => 1,
            _ => 0
        };

        var targetMonth = new DateTime(today.Year, today.Month, 1).AddMonths(monthOffset);
        if (day > DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month))
            return null;

        var targetDate = new DateTime(targetMonth.Year, targetMonth.Month, day);
        if (relativeText == "này" && targetDate.Date < today)
            return targetDate.AddMonths(1);

        return targetDate.Date;
    }

    private static DateTime GetVietnamToday()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        }
        catch
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
            }
            catch
            {
                return DateTime.UtcNow.AddHours(7).Date;
            }
        }
    }

    private async Task<string> BuildAvailabilityContextAsync(ConversationSession session, IntentAnalysis intent)
    {
        if (!session.BookingDraft.CheckInDate.HasValue || !session.BookingDraft.CheckOutDate.HasValue)
            return "";

        try
        {
            var availableRooms = await _hotelData.GetAvailableRoomsAsync(
                session.HotelId,
                session.BookingDraft.CheckInDate.Value,
                session.BookingDraft.CheckOutDate.Value,
                session.BookingDraft.NumAdults ?? 2,
                session.BookingDraft.NumChildren ?? 0);

            if (!availableRooms.Any())
                return "Hiện tại KHÔNG CÒN PHÒNG TRỐNG cho khoảng thời gian này. Hãy đề xuất khách chọn ngày khác.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Khoảng thời gian: {session.BookingDraft.CheckInDate:dd/MM/yyyy} - {session.BookingDraft.CheckOutDate:dd/MM/yyyy}");
            sb.AppendLine($"Số đêm: {(session.BookingDraft.CheckOutDate.Value - session.BookingDraft.CheckInDate.Value).Days}");
            sb.AppendLine("Phòng còn trống:");

            foreach (var room in availableRooms)
            {
                var price = await _hotelData.CalculatePriceAsync(
                    session.HotelId, room.RoomType,
                    session.BookingDraft.CheckInDate.Value,
                    session.BookingDraft.CheckOutDate.Value);

                sb.AppendLine($"- {room.RoomTypeName} (Phòng {room.RoomNumber}): {price:N0} VND tổng {(session.BookingDraft.CheckOutDate.Value - session.BookingDraft.CheckInDate.Value).Days} đêm");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building availability context");
            return "";
        }
    }

    private async Task<List<RoomSuggestionDto>?> BuildRoomSuggestionsAsync(
        ConversationSession session, IntentAnalysis intent, string hotelId)
    {
        if (!session.BookingDraft.CheckInDate.HasValue || !session.BookingDraft.CheckOutDate.HasValue)
            return null;

        try
        {
            var availableRooms = await _hotelData.GetAvailableRoomsAsync(
                hotelId,
                session.BookingDraft.CheckInDate.Value,
                session.BookingDraft.CheckOutDate.Value,
                session.BookingDraft.NumAdults ?? 2,
                session.BookingDraft.NumChildren ?? 0);

            var nights = (session.BookingDraft.CheckOutDate.Value - session.BookingDraft.CheckInDate.Value).Days;
            var suggestions = new List<RoomSuggestionDto>();

            foreach (var room in availableRooms.Take(5))
            {
                var totalPrice = await _hotelData.CalculatePriceAsync(
                    hotelId, room.RoomType,
                    session.BookingDraft.CheckInDate.Value,
                    session.BookingDraft.CheckOutDate.Value);

                var amenities = await _hotelData.GetAmenitiesForRoomAsync(hotelId, room.Amenities);

                suggestions.Add(new RoomSuggestionDto
                {
                    RoomId = room.RoomId,
                    RoomNumber = room.RoomNumber,
                    RoomType = room.RoomType,
                    RoomTypeName = room.RoomTypeName,
                    Description = room.ShortDescription,
                    PricePerNight = nights > 0 ? totalPrice / nights : totalPrice,
                    TotalPrice = totalPrice,
                    View = room.View,
                    BedType = room.BedType,
                    MaxOccupancy = room.MaxOccupancy,
                    KeyAmenities = amenities.Take(5).Select(a => a.VietnameseName).ToList()
                });
            }

            return suggestions;
        }
        catch
        {
            return null;
        }
    }

    private bool IsBookingDraftComplete(BookingDraftDto draft)
    {
        return !string.IsNullOrEmpty(draft.GuestName)
            && !string.IsNullOrEmpty(draft.GuestPhone)
            && draft.CheckInDate.HasValue
            && draft.CheckOutDate.HasValue
            && draft.NumAdults.HasValue
            && !string.IsNullOrEmpty(draft.RoomId);
    }

    private bool CanResolveRoom(BookingDraftDto draft)
    {
        return draft.CheckInDate.HasValue
            && draft.CheckOutDate.HasValue
            && draft.NumAdults.HasValue
            && !string.IsNullOrWhiteSpace(draft.RoomType);
    }

    private async Task TryResolveRoomIdAsync(ConversationSession session, string hotelId)
    {
        var draft = session.BookingDraft;

        if (!string.IsNullOrWhiteSpace(draft.RoomId) || !CanResolveRoom(draft))
            return;

        try
        {
            var availableRooms = await _hotelData.GetAvailableRoomsAsync(
                hotelId,
                draft.CheckInDate!.Value,
                draft.CheckOutDate!.Value,
                draft.NumAdults!.Value,
                draft.NumChildren ?? 0);

            if (!availableRooms.Any())
                return;

            var preferredType = NormalizeRoomValue(draft.RoomType!);
            var matchedRoom = availableRooms.FirstOrDefault(r =>
                   NormalizeRoomValue(r.RoomId) == preferredType
                || NormalizeRoomValue(r.RoomType) == preferredType
                || NormalizeRoomValue(r.RoomTypeName) == preferredType
                || NormalizeRoomValue(r.RoomTypeName).Contains(preferredType)
                || preferredType.Contains(NormalizeRoomValue(r.RoomType)))
                ?? availableRooms.FirstOrDefault(r =>
                    NormalizeRoomValue(r.RoomType) == preferredType);

            if (matchedRoom == null)
                return;

            draft.RoomId = matchedRoom.RoomId;
            draft.RoomType ??= matchedRoom.RoomType;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving room id for session {SessionId}", session.SessionId);
        }
    }

    private string BuildBookingPendingMessage(BookingDraftDto draft)
    {
        var missingFields = GetMissingBookingFields(draft);

        if (!missingFields.Any())
            return "Dạ em đã ghi nhận xác nhận đặt phòng của anh/chị, nhưng hiện chưa thể hoàn tất booking trên hệ thống. Anh/chị vui lòng thử lại trong giây lát nhé.";

        return $"Dạ em chưa thể chốt booking trên hệ thống vì còn thiếu: {string.Join(", ", missingFields)}. Anh/chị giúp em bổ sung để em tạo booking chính xác ngay nhé.";
    }

    private List<string> GetMissingBookingFields(BookingDraftDto draft)
    {
        var missing = new List<string>();

        if (!draft.CheckInDate.HasValue) missing.Add("ngày check-in");
        if (!draft.CheckOutDate.HasValue) missing.Add("ngày check-out");
        if (!draft.NumAdults.HasValue) missing.Add("số lượng khách");
        if (string.IsNullOrWhiteSpace(draft.RoomType) && string.IsNullOrWhiteSpace(draft.RoomId)) missing.Add("loại phòng");
        if (string.IsNullOrWhiteSpace(draft.GuestName)) missing.Add("họ tên");
        if (string.IsNullOrWhiteSpace(draft.GuestPhone)) missing.Add("số điện thoại");
        if (string.IsNullOrWhiteSpace(draft.RoomId) && !string.IsNullOrWhiteSpace(draft.RoomType)) missing.Add("phòng cụ thể còn trống phù hợp");

        return missing;
    }

    private static string NormalizeRoomValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var chars = value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }

    private CreateBookingRequest BuildBookingRequest(ConversationSession session, string hotelId)
    {
        var draft = session.BookingDraft;
        return new CreateBookingRequest
        {
            SessionId = session.SessionId,
            HotelId = hotelId,
            GuestName = draft.GuestName!,
            GuestPhone = draft.GuestPhone!,
            GuestEmail = draft.GuestEmail,
            RoomId = draft.RoomId!,
            CheckInDate = draft.CheckInDate!.Value,
            CheckOutDate = draft.CheckOutDate!.Value,
            NumAdults = draft.NumAdults!.Value,
            NumChildren = draft.NumChildren ?? 0,
            SpecialRequests = draft.SpecialRequests,
            PromoCode = draft.PromoCode,
            PaymentMethod = "Cash"
        };
    }
}
