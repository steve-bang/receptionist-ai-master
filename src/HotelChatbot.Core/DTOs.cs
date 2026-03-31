namespace HotelChatbot.Core.DTOs;

// ============================================================
// CHAT DTOs
// ============================================================
public class ChatRequest
{
    public string SessionId { get; set; } = "";
    public string Message { get; set; } = "";
    public string HotelId { get; set; } = "default";
    public string? Language { get; set; } = "vi"; // vi, en
}

public class ChatResponse
{
    public string SessionId { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Intent { get; set; }           // info_request, booking_init, booking_confirm, availability_check
    public BookingDraftDto? BookingDraft { get; set; }
    public List<RoomSuggestionDto>? RoomSuggestions { get; set; }
    public bool IsBookingComplete { get; set; }
    public string? BookingId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class BookingDraftDto
{
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestEmail { get; set; }
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public int? NumAdults { get; set; }
    public int? NumChildren { get; set; }
    public string? RoomType { get; set; }
    public string? RoomId { get; set; }
    public string? SpecialRequests { get; set; }
    public string? PromoCode { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public string CompletionPercentage => CalculateCompletion();

    private string CalculateCompletion()
    {
        int filled = 0;
        int total = 7;
        if (!string.IsNullOrEmpty(GuestName)) filled++;
        if (!string.IsNullOrEmpty(GuestPhone)) filled++;
        if (CheckInDate.HasValue) filled++;
        if (CheckOutDate.HasValue) filled++;
        if (NumAdults.HasValue) filled++;
        if (!string.IsNullOrEmpty(RoomType) || !string.IsNullOrEmpty(RoomId)) filled++;
        if (!string.IsNullOrEmpty(GuestEmail)) filled++;
        return $"{(filled * 100 / total)}%";
    }
}

public class RoomSuggestionDto
{
    public string RoomId { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public string RoomType { get; set; } = "";
    public string RoomTypeName { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal PricePerNight { get; set; }
    public decimal TotalPrice { get; set; }
    public string View { get; set; } = "";
    public string BedType { get; set; } = "";
    public int MaxOccupancy { get; set; }
    public List<string> KeyAmenities { get; set; } = new();
    public string WhyRecommend { get; set; } = ""; // AI generated recommendation reason
}

// ============================================================
// BOOKING DTOs
// ============================================================
public class CreateBookingRequest
{
    public string SessionId { get; set; } = "";
    public string HotelId { get; set; } = "default";
    public string GuestName { get; set; } = "";
    public string GuestPhone { get; set; } = "";
    public string? GuestEmail { get; set; }
    public string? GuestIdCard { get; set; }
    public string RoomId { get; set; } = "";
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumAdults { get; set; }
    public int NumChildren { get; set; } = 0;
    public string? SpecialRequests { get; set; }
    public string? PromoCode { get; set; }
    public bool BreakfastIncluded { get; set; } = false;
    public string PaymentMethod { get; set; } = "Cash";
}

public class BookingConfirmationDto
{
    public string BookingId { get; set; } = "";
    public string GuestName { get; set; } = "";
    public string RoomTypeName { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int TotalNights { get; set; }
    public int NumAdults { get; set; }
    public int NumChildren { get; set; }
    public decimal RoomRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public bool BreakfastIncluded { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string CheckInTime { get; set; } = "14:00";
    public string CheckOutTime { get; set; } = "12:00";
    public string HotelPhone { get; set; } = "";
    public string HotelAddress { get; set; } = "";
    public string ConfirmationMessage { get; set; } = "";
}

// ============================================================
// AVAILABILITY DTOs
// ============================================================
public class AvailabilityRequest
{
    public string HotelId { get; set; } = "default";
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumAdults { get; set; } = 2;
    public int NumChildren { get; set; } = 0;
    public string? PreferredRoomType { get; set; }
}

public class AvailabilityResponse
{
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int TotalNights { get; set; }
    public List<AvailableRoomDto> AvailableRooms { get; set; } = new();
    public bool HasAvailability => AvailableRooms.Count > 0;
}

public class AvailableRoomDto
{
    public string RoomId { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public string RoomType { get; set; } = "";
    public string RoomTypeName { get; set; } = "";
    public string Description { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public double SizeM2 { get; set; }
    public int MaxOccupancy { get; set; }
    public string BedType { get; set; } = "";
    public string View { get; set; } = "";
    public decimal PricePerNight { get; set; }
    public decimal TotalPrice { get; set; }
    public List<string> Amenities { get; set; } = new();
    public bool BreakfastIncluded { get; set; }
}

// ============================================================
// CONVERSATION SESSION
// ============================================================
public enum BookingSessionStatus
{
    None,        // Chưa có booking trong session này
    InProgress,  // Đang trong quá trình thu thập thông tin booking
    Completed    // Booking đã được tạo thành công — không cho phép tạo lại
}

public class ConversationSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string HotelId { get; set; } = "default";
    public List<ConversationMessage> Messages { get; set; } = new();
    public BookingDraftDto BookingDraft { get; set; } = new();
    public string CurrentIntent { get; set; } = "general";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public string Language { get; set; } = "vi";

    // Hậu-booking state guard — ngăn tạo booking trùng lặp trong cùng session
    public BookingSessionStatus BookingStatus { get; set; } = BookingSessionStatus.None;
    public string? LastBookingId { get; set; }
}

public class ConversationMessage
{
    public string Role { get; set; } = ""; // user, assistant
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// ============================================================
// AI USAGE DTOs
// ============================================================
public class AiUsageSummaryResponse
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public decimal VndPerUsd { get; set; }
    public AiUsageAggregateDto Totals { get; set; } = new();
    public AiUsageMessageEstimateDto AveragePerUserMessage { get; set; } = new();
    public List<AiUsageAggregateByDateDto> ByDay { get; set; } = new();
    public List<AiUsageAggregateByKeyDto> ByHotel { get; set; } = new();
    public List<AiUsageAggregateByKeyDto> ByModel { get; set; } = new();
    public List<AiUsageAggregateByKeyDto> ByOperation { get; set; } = new();
}

public class AiUsageAggregateDto
{
    public int LogsCount { get; set; }
    public int UserMessagesCount { get; set; }
    public int SessionsCount { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
    public decimal CostUsd { get; set; }
    public decimal CostVnd { get; set; }
}

public class AiUsageAggregateByDateDto : AiUsageAggregateDto
{
    public DateTime DateUtc { get; set; }
}

public class AiUsageAggregateByKeyDto : AiUsageAggregateDto
{
    public string Key { get; set; } = "";
}

public class AiUsageMessageEstimateDto
{
    public decimal AvgCostUsdAllOperations { get; set; }
    public decimal AvgCostVndAllOperations { get; set; }
    public decimal AvgPromptTokensAllOperations { get; set; }
    public decimal AvgCompletionTokensAllOperations { get; set; }
    public decimal AvgTotalTokensAllOperations { get; set; }
    public decimal AvgChatReplyPromptTokens { get; set; }
    public decimal AvgChatReplyCompletionTokens { get; set; }
    public decimal AvgChatReplyTotalTokens { get; set; }
    public decimal AvgIntentPromptTokens { get; set; }
    public decimal AvgIntentCompletionTokens { get; set; }
    public decimal AvgEmbeddingPromptTokens { get; set; }
    public decimal AvgSystemPromptChars { get; set; }
    public decimal AvgHistoryChars { get; set; }
    public decimal AvgUserMessageChars { get; set; }
    public decimal AvgOutputChars { get; set; }
}

// ============================================================
// MESSENGER DTOs
// ============================================================
public class MessengerWebhookEventDto
{
    public string PageId { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string RecipientId { get; set; } = "";
    public long TimestampMs { get; set; }
    public string MessageId { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsEcho { get; set; }
    public string PayloadJson { get; set; } = "";
}
