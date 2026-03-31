using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Models;
using HotelChatbot.Core.RAG;

namespace HotelChatbot.Core.Interfaces;

public interface IGoogleSheetsService
{
    Task<HotelInfo?> GetHotelInfoAsync(string hotelId);
    Task<List<Room>> GetRoomsAsync(string hotelId);
    Task<List<RoomPricing>> GetPricingAsync(string hotelId);
    Task<List<RoomAvailability>> GetAvailabilityAsync(string hotelId, DateTime checkIn, DateTime checkOut);
    Task<List<Amenity>> GetAmenitiesAsync(string hotelId);
    Task<List<Promotion>> GetActivePromotionsAsync(string hotelId);
    Task<List<FAQ>> GetFAQsAsync(string hotelId);
    Task<List<Holiday>> GetHolidaysAsync(string hotelId);
    Task AppendAiUsageLogAsync(AiUsageLog log);
    Task<List<AiUsageLog>> GetAiUsageLogsAsync(DateTime fromUtc, DateTime toUtc, string? hotelId = null);
    Task<string> CreateBookingAsync(Booking booking);
    Task<bool> UpdateAvailabilityAsync(string hotelId, string roomId, DateTime checkIn, DateTime checkOut, string bookingId);
    Task<Booking?> GetBookingAsync(string bookingId);
    Task<bool> CancelBookingAsync(string bookingId, string reason);
}

public interface IHotelDataService
{
    Task<HotelInfo?> GetHotelInfoAsync(string hotelId);
    Task<List<Room>> GetAvailableRoomsAsync(string hotelId, DateTime checkIn, DateTime checkOut, int numAdults, int numChildren);
    Task<decimal> CalculatePriceAsync(string hotelId, string roomType, DateTime checkIn, DateTime checkOut);
    Task<RoomPricing?> GetPricingForRoomAsync(string hotelId, string roomType, DateTime checkIn, DateTime checkOut);
    Task<List<Promotion>> GetApplicablePromotionsAsync(string hotelId, string roomType, DateTime checkIn, DateTime checkOut, int nights);
    Task<decimal> ApplyPromoCodeAsync(string hotelId, string promoCode, string roomType, decimal originalPrice);
    Task<List<Amenity>> GetAmenitiesForRoomAsync(string hotelId, string amenityIds);
    Task<string> BuildHotelContextAsync(string hotelId);
}

public interface IClaudeAIService
{
    Task<string> GetChatCompletionAsync(
        string systemPrompt,
        List<ConversationMessage> history,
        string userMessage,
        string operation = "chat_reply",
        string? hotelId = null,
        string? sessionId = null);
    Task<string> BuildSystemPromptAsync(string hotelId);
    Task<IntentAnalysis> AnalyzeIntentAsync(string message, ConversationSession session);
}

public interface IHotelAIService
{
    Task<string> GetChatCompletionAsync(
        string systemPrompt,
        List<ConversationMessage> history,
        string userMessage,
        string operation = "chat_reply",
        string? hotelId = null,
        string? sessionId = null);
    Task<string> BuildSystemPromptAsync(string hotelId);
    Task<IntentAnalysis> AnalyzeIntentAsync(string message, ConversationSession session);
}

public interface IBookingService
{
    Task<BookingConfirmationDto> CreateBookingAsync(CreateBookingRequest request);
    Task<Booking?> GetBookingByIdAsync(string bookingId);
    Task<bool> CancelBookingAsync(string bookingId, string reason);
    Task<bool> ValidateBookingDataAsync(CreateBookingRequest request);
    string GenerateBookingId();
}

public interface IConversationService
{
    Task<ChatResponse> ProcessMessageAsync(ChatRequest request);
    ConversationSession GetOrCreateSession(string sessionId, string hotelId);
    void UpdateSession(ConversationSession session);
    void AddMessageToSession(string sessionId, string role, string content);
}

public interface IRagContextService
{
    Task<string> BuildKnowledgeContextAsync(string hotelId, string query, string? intent = null, string? sessionId = null);
}

public interface IKnowledgeIndexingService
{
    Task<int> ReindexHotelAsync(string hotelId);
    Task<List<KnowledgeDocument>> BuildKnowledgeDocumentsAsync(string hotelId);
}

public interface IEmbeddingService
{
    Task<List<float>> CreateEmbeddingAsync(
        string input,
        string operation = "embedding_query",
        string? hotelId = null,
        string? sessionId = null);
}

public interface IVectorStoreService
{
    Task EnsureCollectionExistsAsync();
    Task UpsertAsync(IEnumerable<KnowledgeChunk> chunks);
    Task DeleteHotelAsync(string hotelId);
    Task<List<KnowledgeSearchResult>> SearchAsync(KnowledgeSearchRequest request);
}

public interface IAIUsageService
{
    Task TrackUsageAsync(AiUsageLog log);
    Task<AiUsageSummaryResponse> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, string? hotelId = null);
}

public interface IMessengerWebhookService
{
    bool ValidateWebhookSubscription(string? mode, string? verifyToken, out string challenge, string? hubChallenge = null);
    Task HandleWebhookAsync(string payloadJson);
}

public class IntentAnalysis
{
    public string Intent { get; set; } = "general"; // general, availability_check, booking_init, booking_confirm, price_inquiry, policy_inquiry, amenity_inquiry, promotion_inquiry, cancel_booking
    public double Confidence { get; set; }
    public Dictionary<string, string> ExtractedEntities { get; set; } = new();
    // Entities: check_in_date, check_out_date, num_adults, num_children, room_type, guest_name, guest_phone, promo_code
}
