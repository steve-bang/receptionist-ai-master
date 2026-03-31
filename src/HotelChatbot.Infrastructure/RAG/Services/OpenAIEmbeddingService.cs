using System.Text;
using System.Text.Json;
using HotelChatbot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelChatbot.Infrastructure.RAG.Services;

public class OpenAIEmbeddingService : IEmbeddingService
{
    private const string EMBEDDINGS_API_URL = "https://api.openai.com/v1/embeddings";

    private readonly HttpClient _httpClient;
    private readonly OpenAIEmbeddingOptions _options;
    private readonly IAIUsageService _aiUsageService;
    private readonly ILogger<OpenAIEmbeddingService> _logger;

    public OpenAIEmbeddingService(
        HttpClient httpClient,
        IOptions<OpenAIEmbeddingOptions> options,
        IAIUsageService aiUsageService,
        ILogger<OpenAIEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _aiUsageService = aiUsageService;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Clear();
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");
    }

    public async Task<List<float>> CreateEmbeddingAsync(
        string input,
        string operation = "embedding_query",
        string? hotelId = null,
        string? sessionId = null)
    {
        var requestBody = new
        {
            model = _options.Model,
            input
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(EMBEDDINGS_API_URL, content);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("OpenAI embeddings error: {StatusCode} - {Error}", response.StatusCode, error);
            throw new InvalidOperationException("Không thể tạo embeddings từ OpenAI.");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        await LogUsageAsync(responseJson, operation, hotelId, sessionId, input);
        using var doc = JsonDocument.Parse(responseJson);

        var embedding = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToList();

        return embedding;
    }

    private async Task LogUsageAsync(string responseJson, string operation, string? hotelId, string? sessionId, string input)
    {
        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("usage", out var usage))
            return;

        var promptTokens = GetInt32OrDefault(usage, "prompt_tokens");
        var totalTokens = GetInt32OrDefault(usage, "total_tokens");

        await _aiUsageService.TrackUsageAsync(new HotelChatbot.Core.Models.AiUsageLog
        {
            TimestampUtc = DateTime.UtcNow,
            HotelId = hotelId ?? "",
            SessionId = sessionId ?? "",
            Provider = "OpenAI",
            Model = _options.Model,
            Operation = operation,
            PromptTokens = promptTokens,
            CompletionTokens = 0,
            TotalTokens = totalTokens,
            InputTextChars = input.Length
        });

        _logger.LogInformation(
            "OpenAI usage tracked: operation={Operation}, hotelId={HotelId}, sessionId={SessionId}, model={Model}, prompt_tokens={PromptTokens}, completion_tokens={CompletionTokens}, total_tokens={TotalTokens}",
            operation,
            hotelId ?? "",
            sessionId ?? "",
            _options.Model,
            promptTokens,
            0,
            totalTokens);
    }

    private static int GetInt32OrDefault(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
            return 0;

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : 0;
    }
}
