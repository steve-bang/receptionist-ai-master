namespace HotelChatbot.Infrastructure.Services;

public class MessengerOptions
{
    public bool Enabled { get; set; }
    public string VerifyToken { get; set; } = "";
    public string PageAccessToken { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string DefaultHotelId { get; set; } = "default";
}
