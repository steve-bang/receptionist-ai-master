namespace HotelChatbot.Infrastructure.RAG;

public class RagOptions
{
    public bool Enabled { get; set; }
    public int TopK { get; set; } = 5;
    public double ScoreThreshold { get; set; } = 0.65;
    public bool UseIntentFilter { get; set; } = true;
    public bool UseRagForChat { get; set; } = true;
    public bool ReplaceFullHotelContext { get; set; }
}
