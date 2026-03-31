namespace HotelChatbot.Core.RAG;

public class KnowledgeDocument
{
    public string HotelId { get; set; } = "";
    public string DocType { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Language { get; set; } = "vi";
    public string? RoomType { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public string SourceSheet { get; set; } = "";
    public string SourceRowKey { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
}

public class KnowledgeChunk
{
    public string PointId { get; set; } = "";
    public List<float> Vector { get; set; } = new();
    public string HotelId { get; set; } = "";
    public string ChunkId { get; set; } = "";
    public string DocType { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Language { get; set; } = "vi";
    public string? RoomType { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public string SourceSheet { get; set; } = "";
    public string SourceRowKey { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
}

public class KnowledgeSearchRequest
{
    public string HotelId { get; set; } = "";
    public string Query { get; set; } = "";
    public List<float> QueryVector { get; set; } = new();
    public string? Intent { get; set; }
    public int Limit { get; set; } = 5;
    public double? ScoreThreshold { get; set; }
}

public class KnowledgeSearchResult
{
    public double Score { get; set; }
    public KnowledgeChunk Chunk { get; set; } = new();
}
