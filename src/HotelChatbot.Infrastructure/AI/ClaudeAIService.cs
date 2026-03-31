using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelChatbot.Infrastructure.AI;

public class ClaudeAIOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-opus-4-5";
    public int MaxTokens { get; set; } = 1024;
    public double Temperature { get; set; } = 0.7;
}

public class ClaudeAIService : HotelAIServiceBase, IClaudeAIService
{
    private readonly HttpClient _httpClient;
    private readonly ClaudeAIOptions _options;
    private readonly ILogger<ClaudeAIService> _logger;

    private const string ANTHROPIC_API_URL = "https://api.anthropic.com/v1/messages";
    private const string ANTHROPIC_VERSION = "2023-06-01";

    public ClaudeAIService(
        HttpClient httpClient,
        IOptions<ClaudeAIOptions> options,
        IHotelDataService hotelData,
        ILogger<ClaudeAIService> logger)
        : base(hotelData)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", ANTHROPIC_VERSION);
    }

    // ============================================================
    // GET CHAT COMPLETION
    // ============================================================
    public override async Task<string> GetChatCompletionAsync(
        string systemPrompt,
        List<ConversationMessage> history,
        string userMessage,
        string operation = "chat_reply",
        string? hotelId = null,
        string? sessionId = null)
    {
        var messages = history.Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToList<object>();

        messages.Add(new { role = "user", content = userMessage });

        var requestBody = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            system = systemPrompt,
            messages
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(ANTHROPIC_API_URL, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Claude API error: {StatusCode} - {Error}", response.StatusCode, error);
                return "Dạ, em đang gặp sự cố kỹ thuật nhỏ. Anh/chị vui lòng thử lại sau giây lát nhé! 🙏";
            }

            var result = await response.Content.ReadFromJsonAsync<ClaudeApiResponse>();
            return result?.Content?.FirstOrDefault()?.Text ?? "Em chưa hiểu ý anh/chị, anh/chị có thể nói rõ hơn không ạ? 😊";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Claude API");
            return "Dạ, em đang gặp sự cố kết nối. Anh/chị vui lòng thử lại sau ít phút nhé! 🙏";
        }
    }

    private class ClaudeApiResponse
    {
        public List<ContentBlock>? Content { get; set; }
    }

    private class ContentBlock
    {
        public string? Text { get; set; }
        public string? Type { get; set; }
    }
}
