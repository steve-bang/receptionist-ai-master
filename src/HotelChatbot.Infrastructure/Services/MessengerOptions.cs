namespace HotelChatbot.Infrastructure.Services;

public class MessengerOptions
{
    public bool Enabled { get; set; }
    public string VerifyToken { get; set; } = "";
    public string PageAccessToken { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string DefaultHotelId { get; set; } = "default";
    // Multi-hotel mapping: Facebook pageId → hotelId
    // Nếu để trống, mọi page đều dùng DefaultHotelId (single-hotel mode)
    public Dictionary<string, string> PageHotelMapping { get; set; } = new();
    // Multi-page access token: Facebook pageId → PageAccessToken
    // Nếu để trống, dùng chung PageAccessToken ở trên
    public Dictionary<string, string> PageAccessTokenMapping { get; set; } = new();
}
