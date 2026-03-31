using System.Text;
using System.Text.Json;
using HotelChatbot.Core.Interfaces;
using HotelChatbot.Core.RAG;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelChatbot.Infrastructure.RAG.Services;

public class QdrantVectorStoreService : IVectorStoreService
{
    private readonly HttpClient _httpClient;
    private readonly QdrantOptions _options;
    private readonly RagOptions _ragOptions;
    private readonly ILogger<QdrantVectorStoreService> _logger;

    public QdrantVectorStoreService(
        HttpClient httpClient,
        IOptions<QdrantOptions> options,
        IOptions<RagOptions> ragOptions,
        ILogger<QdrantVectorStoreService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _ragOptions = ragOptions.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Clear();
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _httpClient.DefaultRequestHeaders.Add("api-key", _options.ApiKey);
    }

    public async Task EnsureCollectionExistsAsync()
    {
        if (!_ragOptions.Enabled)
            return;

        var getResponse = await _httpClient.GetAsync($"collections/{_options.CollectionName}");
        if (getResponse.IsSuccessStatusCode)
            return;

        var payload = new
        {
            vectors = new
            {
                size = _options.VectorSize,
                distance = "Cosine"
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var createResponse = await _httpClient.PutAsync($"collections/{_options.CollectionName}", content);
        if (!createResponse.IsSuccessStatusCode)
        {
            var error = await createResponse.Content.ReadAsStringAsync();
            _logger.LogError("Qdrant create collection error: {Error}", error);
            throw new InvalidOperationException("Không thể tạo collection Qdrant.");
        }
    }

    public async Task UpsertAsync(IEnumerable<KnowledgeChunk> chunks)
    {
        if (!_ragOptions.Enabled)
            return;

        var points = chunks.Select(chunk => new
        {
            id = chunk.PointId,
            vector = chunk.Vector,
            payload = new
            {
                tenant = "prod",
                hotel_id = chunk.HotelId,
                doc_type = chunk.DocType,
                entity_type = chunk.EntityType,
                entity_id = chunk.EntityId,
                chunk_id = chunk.ChunkId,
                language = chunk.Language,
                title = chunk.Title,
                text = chunk.Text,
                room_type = chunk.RoomType,
                category = chunk.Category,
                tags = chunk.Tags,
                keywords = chunk.Keywords,
                priority = chunk.Priority,
                is_active = chunk.IsActive,
                source_sheet = chunk.SourceSheet,
                source_row_key = chunk.SourceRowKey,
                updated_at = chunk.UpdatedAtUtc,
                version = chunk.Version
            }
        }).ToList();

        var requestBody = new { points };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync($"collections/{_options.CollectionName}/points", content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Qdrant upsert error: {Error}", error);
            throw new InvalidOperationException("Không thể upsert points vào Qdrant.");
        }
    }

    public async Task DeleteHotelAsync(string hotelId)
    {
        if (!_ragOptions.Enabled)
            return;

        var requestBody = new
        {
            filter = new
            {
                must = new object[]
                {
                    new
                    {
                        key = "hotel_id",
                        match = new
                        {
                            value = hotelId
                        }
                    }
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"collections/{_options.CollectionName}/points/delete")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Qdrant delete hotel error: {Error}", error);
            throw new InvalidOperationException("Không thể xóa points của khách sạn trong Qdrant.");
        }
    }

    public async Task<List<KnowledgeSearchResult>> SearchAsync(KnowledgeSearchRequest request)
    {
        if (!_ragOptions.Enabled)
            return new List<KnowledgeSearchResult>();

        var requestBody = new
        {
            vector = request.QueryVector,
            limit = request.Limit,
            with_payload = true,
            filter = new
            {
                must = BuildMustFilters(request)
            },
            score_threshold = request.ScoreThreshold
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"collections/{_options.CollectionName}/points/search", content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Qdrant search error: {Error}", error);
            return new List<KnowledgeSearchResult>();
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
            return new List<KnowledgeSearchResult>();

        var searchResults = new List<KnowledgeSearchResult>();
        foreach (var item in result.EnumerateArray())
        {
            var payload = item.GetProperty("payload");
            searchResults.Add(new KnowledgeSearchResult
            {
                Score = item.TryGetProperty("score", out var score) ? score.GetDouble() : 0,
                Chunk = new KnowledgeChunk
                {
                    PointId = item.GetProperty("id").ToString(),
                    HotelId = GetPayloadString(payload, "hotel_id"),
                    ChunkId = GetPayloadString(payload, "chunk_id"),
                    DocType = GetPayloadString(payload, "doc_type"),
                    EntityType = GetPayloadString(payload, "entity_type"),
                    EntityId = GetPayloadString(payload, "entity_id"),
                    Title = GetPayloadString(payload, "title"),
                    Text = GetPayloadString(payload, "text"),
                    Language = GetPayloadString(payload, "language"),
                    RoomType = GetNullablePayloadString(payload, "room_type"),
                    Category = GetNullablePayloadString(payload, "category"),
                    Tags = GetPayloadStrings(payload, "tags"),
                    Keywords = GetPayloadStrings(payload, "keywords"),
                    SourceSheet = GetPayloadString(payload, "source_sheet"),
                    SourceRowKey = GetPayloadString(payload, "source_row_key"),
                    IsActive = payload.TryGetProperty("is_active", out var isActive) && isActive.GetBoolean(),
                    Priority = payload.TryGetProperty("priority", out var priority) ? priority.GetInt32() : 0
                }
            });
        }

        return searchResults;
    }

    private object[] BuildMustFilters(KnowledgeSearchRequest request)
    {
        var must = new List<object>
        {
            new
            {
                key = "hotel_id",
                match = new
                {
                    value = request.HotelId
                }
            },
            new
            {
                key = "is_active",
                match = new
                {
                    value = true
                }
            }
        };

        return must.ToArray();
    }

    private static string GetPayloadString(JsonElement payload, string key)
        => payload.TryGetProperty(key, out var value) ? value.ToString() : "";

    private static string? GetNullablePayloadString(JsonElement payload, string key)
        => payload.TryGetProperty(key, out var value) ? value.ToString() : null;

    private static List<string> GetPayloadStrings(JsonElement payload, string key)
    {
        if (!payload.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return value.EnumerateArray().Select(x => x.ToString()).ToList();
    }
}
