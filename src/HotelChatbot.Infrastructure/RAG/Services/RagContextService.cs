using HotelChatbot.Core.Interfaces;
using HotelChatbot.Core.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelChatbot.Infrastructure.RAG.Services;

public class RagContextService : IRagContextService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly RagOptions _options;
    private readonly ILogger<RagContextService> _logger;

    public RagContextService(
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IOptions<RagOptions> options,
        ILogger<RagContextService> logger)
    {
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> BuildKnowledgeContextAsync(string hotelId, string query, string? intent = null, string? sessionId = null)
    {
        if (!_options.Enabled || !_options.UseRagForChat || string.IsNullOrWhiteSpace(query))
            return "";

        try
        {
            var embedding = await _embeddingService.CreateEmbeddingAsync(
                query,
                "embedding_query",
                hotelId,
                sessionId);
            var results = await _vectorStoreService.SearchAsync(new KnowledgeSearchRequest
            {
                HotelId = hotelId,
                Query = query,
                QueryVector = embedding,
                Intent = intent,
                Limit = _options.TopK,
                ScoreThreshold = _options.ScoreThreshold
            });

            if (!results.Any())
                return "";

            var lines = results
                .OrderByDescending(r => r.Score)
                .Select(r => $"- [{r.Chunk.DocType}] {r.Chunk.Title}: {r.Chunk.Text}");

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building RAG context for hotel {HotelId}", hotelId);
            return "";
        }
    }
}
