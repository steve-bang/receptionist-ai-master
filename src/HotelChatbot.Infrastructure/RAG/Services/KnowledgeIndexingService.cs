using HotelChatbot.Core.Interfaces;
using HotelChatbot.Core.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace HotelChatbot.Infrastructure.RAG.Services;

public class KnowledgeIndexingService : IKnowledgeIndexingService
{
    private readonly HotelKnowledgeDocumentFactory _documentFactory;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly RagOptions _options;
    private readonly ILogger<KnowledgeIndexingService> _logger;

    public KnowledgeIndexingService(
        HotelKnowledgeDocumentFactory documentFactory,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IOptions<RagOptions> options,
        ILogger<KnowledgeIndexingService> logger)
    {
        _documentFactory = documentFactory;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<KnowledgeDocument>> BuildKnowledgeDocumentsAsync(string hotelId)
    {
        return await _documentFactory.BuildAsync(hotelId);
    }

    public async Task<int> ReindexHotelAsync(string hotelId)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("RAG disabled. Skip reindex for hotel {HotelId}", hotelId);
            return 0;
        }

        var documents = await BuildKnowledgeDocumentsAsync(hotelId);
        if (!documents.Any())
            return 0;

        await _vectorStoreService.EnsureCollectionExistsAsync();
        await _vectorStoreService.DeleteHotelAsync(hotelId);

        var chunks = new List<KnowledgeChunk>();
        foreach (var doc in documents)
        {
            var embedding = await _embeddingService.CreateEmbeddingAsync(
                doc.Text,
                "embedding_index",
                hotelId);
            var rawPointKey = $"{doc.HotelId}:{doc.DocType}:{doc.EntityId}:0";
            chunks.Add(new KnowledgeChunk
            {
                PointId = CreateDeterministicPointId(rawPointKey),
                ChunkId = $"{doc.EntityId}#0",
                Vector = embedding,
                HotelId = doc.HotelId,
                DocType = doc.DocType,
                EntityType = doc.EntityType,
                EntityId = doc.EntityId,
                Title = doc.Title,
                Text = doc.Text,
                Language = doc.Language,
                RoomType = doc.RoomType,
                Category = doc.Category,
                Tags = doc.Tags,
                Keywords = doc.Keywords,
                IsActive = doc.IsActive,
                Priority = doc.Priority,
                SourceSheet = doc.SourceSheet,
                SourceRowKey = doc.SourceRowKey,
                UpdatedAtUtc = doc.UpdatedAtUtc,
                Version = doc.Version
            });
        }

        await _vectorStoreService.UpsertAsync(chunks);
        _logger.LogInformation("Reindexed {Count} knowledge chunks for hotel {HotelId}", chunks.Count, hotelId);
        return chunks.Count;
    }

    private static string CreateDeterministicPointId(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes).ToString();
    }
}
