namespace HotelChatbot.Infrastructure.RAG;

public class OpenAIEmbeddingOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "text-embedding-3-small";
}
