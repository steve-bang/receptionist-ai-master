namespace HotelChatbot.Infrastructure.RAG;

public class QdrantOptions
{
    public string BaseUrl { get; set; } = "http://localhost:6333";
    public string ApiKey { get; set; } = "";
    public string CollectionName { get; set; } = "hotel_knowledge_v1";
    public int VectorSize { get; set; } = 1536;
}
