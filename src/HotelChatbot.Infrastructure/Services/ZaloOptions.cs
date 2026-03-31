namespace HotelChatbot.Infrastructure.Services;

public class ZaloOptions
{
    public bool Enabled { get; set; }
    public string DefaultOaId { get; set; } = "";
    public string DefaultAccessToken { get; set; } = "";
    public string DefaultSecretKey { get; set; } = "";
    public string DefaultHotelId { get; set; } = "default";
    // Multi-OA mapping: Zalo OA ID → hotelId
    public Dictionary<string, string> OaHotelMapping { get; set; } = new();
    // Multi-OA access token: Zalo OA ID → AccessToken (hết hạn sau 3h, cần refresh thủ công Phase 1)
    public Dictionary<string, string> OaAccessTokenMapping { get; set; } = new();
    // Multi-OA secret key: Zalo OA ID → SecretKey (dùng để verify webhook signature)
    public Dictionary<string, string> OaSecretKeyMapping { get; set; } = new();
}
