using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using HotelChatbot.Core.Models;
using Microsoft.Extensions.Logging;

namespace HotelChatbot.Infrastructure.Services;

public class HotelDataService : IHotelDataService
{
    private readonly IGoogleSheetsService _sheets;
    private readonly ILogger<HotelDataService> _logger;

    public HotelDataService(IGoogleSheetsService sheets, ILogger<HotelDataService> logger)
    {
        _sheets = sheets;
        _logger = logger;
    }

    public async Task<HotelInfo?> GetHotelInfoAsync(string hotelId)
        => await _sheets.GetHotelInfoAsync(hotelId);

    // ============================================================
    // GET AVAILABLE ROOMS
    // ============================================================
    public async Task<List<Room>> GetAvailableRoomsAsync(
        string hotelId, DateTime checkIn, DateTime checkOut, int numAdults, int numChildren)
    {
        var allRooms = await _sheets.GetRoomsAsync(hotelId);
        var availability = await _sheets.GetAvailabilityAsync(hotelId, checkIn, checkOut);

        // Find rooms that are booked during the requested period
        var bookedRoomIds = availability
            .Where(a => a.Status == "Booked" || a.Status == "Maintenance" || a.Status == "Blocked")
            .Select(a => a.RoomId)
            .ToHashSet();

        // Filter rooms: active, enough capacity, not booked
        var availableRooms = allRooms
            .Where(r => r.IsActive
                && !bookedRoomIds.Contains(r.RoomId)
                && r.MaxAdults >= numAdults
                && (r.MaxChildren >= numChildren || numChildren == 0))
            .ToList();

        return availableRooms;
    }

    // ============================================================
    // CALCULATE PRICE
    // ============================================================
    public async Task<decimal> CalculatePriceAsync(string hotelId, string roomType, DateTime checkIn, DateTime checkOut)
    {
        var pricing = await GetPricingForRoomAsync(hotelId, roomType, checkIn, checkOut);
        if (pricing == null) return 0;

        var holidays = await _sheets.GetHolidaysAsync(hotelId);
        var holidayDates = holidays.Select(h => h.Date.Date).ToHashSet();

        decimal total = 0;
        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            if (holidayDates.Contains(date.Date))
                total += pricing.HolidayPrice > 0 ? pricing.HolidayPrice : pricing.WeekendPrice;
            else if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                total += pricing.WeekendPrice;
            else
                total += pricing.WeekdayPrice;
        }

        return total;
    }

    public async Task<RoomPricing?> GetPricingForRoomAsync(string hotelId, string roomType, DateTime checkIn, DateTime checkOut)
    {
        var pricingList = await _sheets.GetPricingAsync(hotelId);
        var today = DateTime.Today;

        return pricingList
            .Where(p => p.RoomType == roomType
                && (p.ValidFrom == DateTime.MinValue || p.ValidFrom <= today)
                && (p.ValidTo == DateTime.MinValue || p.ValidTo >= today))
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefault();
    }

    // ============================================================
    // GET APPLICABLE PROMOTIONS
    // ============================================================
    public async Task<List<Promotion>> GetApplicablePromotionsAsync(
        string hotelId, string roomType, DateTime checkIn, DateTime checkOut, int nights)
    {
        var allPromos = await _sheets.GetActivePromotionsAsync(hotelId);
        var totalPrice = await CalculatePriceAsync(hotelId, roomType, checkIn, checkOut);

        return allPromos.Where(p =>
        {
            // Check room type
            if (p.ApplicableRoomTypes != "All" && !string.IsNullOrEmpty(p.ApplicableRoomTypes))
            {
                var types = p.ApplicableRoomTypes.Split(',').Select(t => t.Trim());
                if (!types.Contains(roomType)) return false;
            }

            // Check minimum stay
            if (p.MinimumStayNights > 0 && nights < p.MinimumStayNights) return false;

            // Check minimum spend
            if (p.MinimumSpend > 0 && totalPrice < p.MinimumSpend) return false;

            // Check valid dates
            if (p.ValidFrom != DateTime.MinValue && checkIn < p.ValidFrom) return false;
            if (p.ValidTo != DateTime.MinValue && checkOut > p.ValidTo.AddDays(1)) return false;

            // Check usage limit
            if (p.UsageLimit > 0 && p.UsageCount >= p.UsageLimit) return false;

            return true;
        }).ToList();
    }

    // ============================================================
    // APPLY PROMO CODE
    // ============================================================
    public async Task<decimal> ApplyPromoCodeAsync(
        string hotelId, string promoCode, string roomType, decimal originalPrice)
    {
        if (string.IsNullOrEmpty(promoCode)) return 0;

        var promos = await _sheets.GetActivePromotionsAsync(hotelId);
        var promo = promos.FirstOrDefault(p =>
            p.PromoCode.Equals(promoCode, StringComparison.OrdinalIgnoreCase) && p.IsActive);

        if (promo == null) return 0;

        return promo.DiscountType switch
        {
            "Percentage" => Math.Round(originalPrice * promo.DiscountValue / 100, 0),
            "FixedAmount" => Math.Min(promo.DiscountValue, originalPrice),
            _ => 0
        };
    }

    // ============================================================
    // GET AMENITIES FOR ROOM
    // ============================================================
    public async Task<List<Amenity>> GetAmenitiesForRoomAsync(string hotelId, string amenityIds)
    {
        if (string.IsNullOrEmpty(amenityIds)) return new List<Amenity>();

        var allAmenities = await _sheets.GetAmenitiesAsync(hotelId);
        var ids = amenityIds.Split(',').Select(id => id.Trim()).ToHashSet();

        return allAmenities.Where(a => ids.Contains(a.AmenityId) && a.IsActive).ToList();
    }

    // ============================================================
    // BUILD FULL HOTEL CONTEXT FOR AI
    // ============================================================
    public async Task<string> BuildHotelContextAsync(string hotelId)
    {
        var hotel = await _sheets.GetHotelInfoAsync(hotelId);
        var rooms = await _sheets.GetRoomsAsync(hotelId);
        var pricingList = await _sheets.GetPricingAsync(hotelId);
        var amenities = await _sheets.GetAmenitiesAsync(hotelId);
        var promos = await _sheets.GetActivePromotionsAsync(hotelId);
        var faqs = await _sheets.GetFAQsAsync(hotelId);

        var context = new System.Text.StringBuilder();

        if (hotel != null)
        {
            context.AppendLine("=== THÔNG TIN KHÁCH SẠN ===");
            context.AppendLine($"Tên: {hotel.Name}");
            context.AppendLine($"Giới thiệu: {hotel.Description}");
            context.AppendLine($"Địa chỉ: {hotel.Address}");
            context.AppendLine($"Vị trí & Giao thông: {hotel.LocationDescription}");
            context.AppendLine($"Điểm nổi bật lân cận: {hotel.NearbyLandmarks}");
            context.AppendLine($"Điện thoại: {hotel.PhoneNumber}");
            context.AppendLine($"Email: {hotel.Email}");
            context.AppendLine($"Xếp hạng: {hotel.StarRating} sao");
            context.AppendLine($"Ngôn ngữ hỗ trợ: {hotel.Languages}");
            context.AppendLine();
            context.AppendLine("=== CHÍNH SÁCH NHẬN/TRẢ PHÒNG ===");
            context.AppendLine($"Check-in: {hotel.CheckInTime}");
            context.AppendLine($"Check-out: {hotel.CheckOutTime}");
            context.AppendLine($"Check-in sớm: {hotel.EarlyCheckInPolicy}");
            context.AppendLine($"Check-out muộn: {hotel.LateCheckOutPolicy}");
            context.AppendLine($"Hủy phòng: {hotel.CancellationPolicy}");
            context.AppendLine($"Thú cưng: {hotel.PetPolicy}");
            context.AppendLine($"Hút thuốc: {hotel.SmokingPolicy}");
            context.AppendLine($"Trẻ em: {hotel.ChildPolicy}");
            context.AppendLine($"Thanh toán: {hotel.PaymentMethods}");
        }

        context.AppendLine();
        context.AppendLine("=== DANH SÁCH PHÒNG ===");
        foreach (var room in rooms.Where(r => r.IsActive))
        {
            var pricing = pricingList.FirstOrDefault(p => p.RoomType == room.RoomType);
            context.AppendLine($"[{room.RoomType}] {room.RoomTypeName} (Phòng {room.RoomNumber})");
            context.AppendLine($"  - Diện tích: {room.SizeM2}m², Tầng {room.Floor}");
            context.AppendLine($"  - Sức chứa: {room.MaxAdults} người lớn, {room.MaxChildren} trẻ em");
            context.AppendLine($"  - Giường: {room.BedType}, View: {room.View}");
            context.AppendLine($"  - Mô tả: {room.Description}");
            if (pricing != null)
            {
                context.AppendLine($"  - Giá ngày thường: {pricing.WeekdayPrice:N0} VND/đêm");
                context.AppendLine($"  - Giá cuối tuần: {pricing.WeekendPrice:N0} VND/đêm");
                context.AppendLine($"  - Giá ngày lễ: {pricing.HolidayPrice:N0} VND/đêm");
                if (pricing.BreakfastIncluded == "Yes")
                    context.AppendLine($"  - Bao gồm bữa sáng");
                else if (pricing.BreakfastFee > 0)
                    context.AppendLine($"  - Phụ thu bữa sáng: {pricing.BreakfastFee:N0} VND/người");
            }
        }

        context.AppendLine();
        context.AppendLine("=== TIỆN ÍCH KHÁCH SẠN ===");
        foreach (var cat in amenities.Where(a => a.IsActive).GroupBy(a => a.Category))
        {
            context.AppendLine($"[{cat.Key}]");
            foreach (var amenity in cat)
            {
                var feeInfo = amenity.IsComplimentary ? "(Miễn phí)" : $"(Phụ thu: {amenity.Fee:N0} VND)";
                context.AppendLine($"  - {amenity.VietnameseName}: {amenity.Description} {feeInfo}");
                if (!string.IsNullOrEmpty(amenity.OperatingHours))
                    context.AppendLine($"    Giờ hoạt động: {amenity.OperatingHours}");
            }
        }

        if (promos.Count > 0)
        {
            context.AppendLine();
            context.AppendLine("=== ƯU ĐÃI ĐANG CÓ ===");
            foreach (var promo in promos)
            {
                context.AppendLine($"[{promo.PromoCode}] {promo.Title}");
                context.AppendLine($"  - {promo.Description}");
                context.AppendLine($"  - Giảm: {promo.DiscountValue}{(promo.DiscountType == "Percentage" ? "%" : "K VND")}");
                context.AppendLine($"  - Điều kiện: {promo.Conditions}");
                if (promo.ValidTo != DateTime.MinValue)
                    context.AppendLine($"  - Áp dụng đến: {promo.ValidTo:dd/MM/yyyy}");
            }
        }

        if (faqs.Count > 0)
        {
            context.AppendLine();
            context.AppendLine("=== CÂU HỎI THƯỜNG GẶP ===");
            foreach (var faq in faqs.Where(f => f.IsActive).OrderBy(f => f.Priority))
            {
                context.AppendLine($"Q: {faq.Question}");
                context.AppendLine($"A: {faq.Answer}");
            }
        }

        return context.ToString();
    }
}