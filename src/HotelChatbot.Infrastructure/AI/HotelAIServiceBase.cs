using System.Text.Json;
using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;

namespace HotelChatbot.Infrastructure.AI;

public abstract class HotelAIServiceBase : IHotelAIService
{
    private readonly IHotelDataService _hotelData;

    protected HotelAIServiceBase(IHotelDataService hotelData)
    {
        _hotelData = hotelData;
    }

    public async Task<string> BuildSystemPromptAsync(string hotelId)
    {
        var hotelContext = await _hotelData.BuildHotelContextAsync(hotelId);
        var hotel = await _hotelData.GetHotelInfoAsync(hotelId);
        var hotelName = hotel?.Name ?? "khách sạn";

        return $"""
        Bạn là trợ lý AI lễ tân thông minh của {hotelName}. Bạn thay thế hoàn toàn nhân viên lễ tân thật sự.

        ## VAI TRÒ VÀ PHONG CÁCH
        - Tên gọi: "Lễ Tân AI" của {hotelName}
        - Giọng điệu: Thân thiện, dịu dàng, lịch sự theo phong cách người Việt Nam
        - Luôn xưng "em" và gọi khách là "anh/chị" hoặc "quý khách"
        - Câu văn tự nhiên, không cứng nhắc, không máy móc
        - Nhiệt tình tư vấn như lễ tân thật sự, không chỉ trả lời qua loa
        - Dùng emoji vừa phải để thân thiện hơn (🏨 ✨ 🌟 😊 etc.)

        ## NGUYÊN TẮC TƯ VẤN
        1. **Trả lời CHÍNH XÁC** theo thông tin trong hệ thống, KHÔNG bịa đặt giá hay thông tin
        2. **Tư vấn CHỦ ĐỘNG**: Khi khách hỏi giá → luôn kèm gợi ý phòng phù hợp
        3. **UPSELL tự nhiên**: Giới thiệu thêm phòng cao cấp hơn nếu phù hợp ngân sách/nhu cầu
        4. **Nhắc ưu đãi**: Luôn thông báo các khuyến mãi đang có khi tư vấn giá
        5. **Kiểm tra kỹ**: Khi khách muốn đặt phòng, hỏi đủ thông tin trước khi xác nhận

        ## QUY TRÌNH ĐẶT PHÒNG (BOOKING FLOW)
        Khi khách muốn đặt phòng, thu thập THEO THỨ TỰ:
        1. Ngày check-in và check-out
        2. Số lượng khách (người lớn + trẻ em)
        3. Loại phòng mong muốn (gợi ý nếu khách chưa biết)
        4. Họ tên đầy đủ
        5. Số điện thoại liên lạc
        6. Email (nếu có)
        7. Yêu cầu đặc biệt (nếu có)
        8. Mã ưu đãi (nếu có)
        
        Sau khi có đủ thông tin → TÓM TẮT lại toàn bộ và hỏi xác nhận.
        Sau khi khách xác nhận → Thông báo đặt phòng thành công với mã booking.

        ## XỬ LÝ TÌNH HUỐNG ĐẶC BIỆT
        - Khách hỏi phòng trống: Kiểm tra theo ngày và số khách, trả lời cụ thể
        - Khách muốn hủy phòng: Hỏi mã booking, giải thích chính sách hủy
        - Khách khiếu nại: Lắng nghe, đồng cảm, đề xuất giải pháp
        - Câu hỏi không liên quan: Nhẹ nhàng hướng về dịch vụ khách sạn

        ## FORMAT TRẢ LỜI
        - Câu ngắn gọn, dễ đọc trên điện thoại
        - Dùng bullet point khi liệt kê nhiều mục
        - Giá tiền format: 1.500.000 VND hoặc 1.5 triệu
        - Ngày tháng format: ngày 15/06/2025

        ## DỮ LIỆU KHÁCH SẠN (REALTIME)
        {hotelContext}

        ## LƯU Ý QUAN TRỌNG
        - Nếu không có thông tin về điều gì đó → Thành thật nói "em chưa có thông tin về vấn đề này, anh/chị vui lòng liên hệ trực tiếp qua số {hotel?.PhoneNumber}"
        - KHÔNG BAO GIỜ bịa thông tin, đặc biệt về giá cả và phòng trống
        - Khi booking hoàn tất, kết thúc response với JSON marker: [BOOKING_READY]
        """;
    }

    public async Task<IntentAnalysis> AnalyzeIntentAsync(string message, ConversationSession session)
    {
        var now = GetVietnamNow();
        var intentPrompt = """
        Phân tích tin nhắn của khách và trả về JSON theo format sau:
        {
          "intent": "...",
          "confidence": 0.9,
          "entities": {
            "check_in_date": "2025-06-15",
            "check_out_date": "2025-06-17", 
            "num_adults": "2",
            "num_children": "0",
            "room_type": "Deluxe",
            "guest_name": "Nguyễn Văn A",
            "guest_phone": "0901234567",
            "promo_code": "SUMMER25"
          }
        }
        
        Intent có thể là: general, availability_check, booking_init, booking_confirm, booking_cancel, 
        price_inquiry, policy_inquiry, amenity_inquiry, promotion_inquiry, complaint, location_inquiry

        QUAN TRỌNG VỀ NGÀY THÁNG:
        - Hôm nay theo giờ Việt Nam là: {{TODAY_FULL}}
        - Khi khách nói ngày tương đối như "thứ 7 tuần tới", "thứ 2 tuần tới", "ngày 13 tháng tới", "cuối tuần này"
          thì phải quy đổi ra ngày TƯƠNG LAI gần nhất phù hợp theo giờ Việt Nam.
        - KHÔNG được suy ra ngày trong quá khứ nếu khách đang nói về booking mới.
        - Các trường check_in_date, check_out_date phải trả về theo format yyyy-MM-dd.
        
        Chỉ trả về JSON, không có text khác.
        """;
        intentPrompt = intentPrompt.Replace("{{TODAY_FULL}}", $"{now:dddd, dd/MM/yyyy}");

        var contextMessages = new List<ConversationMessage>
        {
            new() { Role = "user", Content = $"Tin nhắn: \"{message}\"\nLịch sử ngắn: {string.Join(" | ", session.Messages.TakeLast(3).Select(m => $"{m.Role}: {m.Content[..Math.Min(50, m.Content.Length)]}"))}" }
        };

        var response = await GetChatCompletionAsync(
            intentPrompt,
            new List<ConversationMessage>(),
            contextMessages[0].Content,
            "intent_analysis",
            session.HotelId,
            session.SessionId);

        try
        {
            var cleanJson = response.Trim();
            if (cleanJson.StartsWith("```"))
                cleanJson = cleanJson.Split('\n').Skip(1).SkipLast(1).Aggregate((a, b) => a + "\n" + b);

            var parsed = JsonSerializer.Deserialize<IntentResponse>(cleanJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new IntentAnalysis
            {
                Intent = parsed?.Intent ?? "general",
                Confidence = parsed?.Confidence ?? 0.5,
                ExtractedEntities = parsed?.Entities ?? new Dictionary<string, string>()
            };
        }
        catch
        {
            return new IntentAnalysis { Intent = "general", Confidence = 0.5 };
        }
    }

    public abstract Task<string> GetChatCompletionAsync(
        string systemPrompt,
        List<ConversationMessage> history,
        string userMessage,
        string operation = "chat_reply",
        string? hotelId = null,
        string? sessionId = null);

    private class IntentResponse
    {
        public string? Intent { get; set; }
        public double Confidence { get; set; }
        public Dictionary<string, string>? Entities { get; set; }
    }

    private static DateTime GetVietnamNow()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                return DateTime.UtcNow.AddHours(7);
            }
        }
    }
}
