namespace HotelChatbot.Core.Models;

// ============================================================
// HOTEL INFO
// ============================================================
public class HotelInfo
{
    public string HotelId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Address { get; set; } = "";
    public string LocationDescription { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string NearbyLandmarks { get; set; } = "";
    public string StarRating { get; set; } = "";
    public string CheckInTime { get; set; } = "14:00";
    public string CheckOutTime { get; set; } = "12:00";
    public string EarlyCheckInPolicy { get; set; } = "";
    public string LateCheckOutPolicy { get; set; } = "";
    public string CancellationPolicy { get; set; } = "";
    public string PetPolicy { get; set; } = "";
    public string SmokingPolicy { get; set; } = "";
    public string ChildPolicy { get; set; } = "";
    public string PaymentMethods { get; set; } = "";
    public string Languages { get; set; } = "";
    public string SocialMedia { get; set; } = "";
}

// ============================================================
// ROOM
// ============================================================
public partial class Room
{
    public string HotelId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public string RoomType { get; set; } = "";         // Standard, Deluxe, Suite, Family, etc.
    public string RoomTypeName { get; set; } = "";     // Tên tiếng Việt hiển thị
    public int Floor { get; set; }
    public double SizeM2 { get; set; }
    public int MaxAdults { get; set; }
    public int MaxChildren { get; set; }
    public int MaxOccupancy { get; set; }
    public string BedType { get; set; } = "";          // King, Queen, Twin, Double
    public string View { get; set; } = "";             // Sea view, City view, Garden view
    public string Description { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string Amenities { get; set; } = "";        // Comma-separated amenity IDs
    public string ImageUrls { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string Tags { get; set; } = "";             // romantic, family-friendly, business
}

// ============================================================
// PRICING
// ============================================================
public partial class RoomPricing
{
    public string HotelId { get; set; } = "";
    public string PricingId { get; set; } = "";
    public string RoomType { get; set; } = "";
    public decimal WeekdayPrice { get; set; }          // Thứ 2 - Thứ 5
    public decimal WeekendPrice { get; set; }          // Thứ 6 - CN
    public decimal HolidayPrice { get; set; }          // Ngày lễ tết
    public decimal PeakSeasonPrice { get; set; }       // Mùa cao điểm
    public string Currency { get; set; } = "VND";
    public string PriceUnit { get; set; } = "per_night";
    public decimal ExtraAdultFee { get; set; }
    public decimal ExtraChildFee { get; set; }
    public string BreakfastIncluded { get; set; } = "No";
    public decimal BreakfastFee { get; set; }
    public string Notes { get; set; } = "";
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

// ============================================================
// AVAILABILITY
// ============================================================
public partial class RoomAvailability
{
    public string HotelId { get; set; } = "";
    public string AvailabilityId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public string RoomType { get; set; } = "";
    public DateTime Date { get; set; }
    public string Status { get; set; } = "Available";  // Available, Booked, Maintenance, Blocked
    public string BookingId { get; set; } = "";
    public string Notes { get; set; } = "";
}

// ============================================================
// AMENITY
// ============================================================
public class Amenity
{
    public string AmenityId { get; set; } = "";
    public string Name { get; set; } = "";
    public string VietnameseName { get; set; } = "";
    public string Category { get; set; } = "";        // InRoom, Hotel, Service, Recreation
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public bool IsComplimentary { get; set; }
    public decimal? Fee { get; set; }
    public string OperatingHours { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

// ============================================================
// PROMOTION
// ============================================================
public partial class Promotion
{
    public string HotelId { get; set; } = "";
    public string PromoId { get; set; } = "";
    public string PromoCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string DiscountType { get; set; } = "";    // Percentage, FixedAmount, FreeNight
    public decimal DiscountValue { get; set; }
    public decimal MinimumStayNights { get; set; }
    public decimal MinimumSpend { get; set; }
    public string ApplicableRoomTypes { get; set; } = ""; // All or comma-separated types
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string BookingWindow { get; set; } = "";   // Book trước bao nhiêu ngày
    public string Conditions { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int UsageLimit { get; set; }
    public int UsageCount { get; set; }
}

// ============================================================
// BOOKING
// ============================================================
public class Booking
{
    public string BookingId { get; set; } = "";
    public string HotelId { get; set; } = "";
    public string GuestName { get; set; } = "";
    public string GuestPhone { get; set; } = "";
    public string GuestEmail { get; set; } = "";
    public string GuestIdCard { get; set; } = "";     // CCCD/Hộ chiếu (optional)
    public string Nationality { get; set; } = "Vietnamese";
    public string RoomId { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public string RoomType { get; set; } = "";
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int TotalNights { get; set; }
    public int NumAdults { get; set; }
    public int NumChildren { get; set; }
    public string SpecialRequests { get; set; } = "";
    public decimal RoomRate { get; set; }
    public decimal TotalAmount { get; set; }
    public string PromoCode { get; set; } = "";
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public bool BreakfastIncluded { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string PaymentStatus { get; set; } = "Pending"; // Pending, Paid, Refunded
    public string BookingStatus { get; set; } = "Confirmed"; // Confirmed, Cancelled, CheckedIn, CheckedOut, NoShow
    public string BookingChannel { get; set; } = "Chatbot";
    public string SessionId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Notes { get; set; } = "";
    public string CancellationReason { get; set; } = "";
}

// ============================================================
// FAQ
// ============================================================
public class FAQ
{
    public string FaqId { get; set; } = "";
    public string Category { get; set; } = "";
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Keywords { get; set; } = "";
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

// ============================================================
// HOLIDAY
// ============================================================
public class Holiday
{
    public string HolidayId { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime Date { get; set; }
    public string PriceType { get; set; } = "Holiday"; // Holiday, PeakSeason
    public string Notes { get; set; } = "";
}

// ============================================================
// HOTEL CONFIG (for multi-hotel support)
// ============================================================
public class HotelConfig
{
    public string HotelId { get; set; } = "";
    public string ConfigKey { get; set; } = "";
    public string ConfigValue { get; set; } = "";
    public string Description { get; set; } = "";
}

// ============================================================
// AI USAGE LOG
// ============================================================
public class AiUsageLog
{
    public string LogId { get; set; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string HotelId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "";
    public string Operation { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public int SystemPromptChars { get; set; }
    public int HistoryChars { get; set; }
    public int UserMessageChars { get; set; }
    public int InputTextChars { get; set; }
    public int OutputChars { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public decimal EstimatedCostVnd { get; set; }
}
