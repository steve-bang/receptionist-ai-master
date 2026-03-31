using System.Text;
using System.Text.Json;
using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelChatbot.Infrastructure.AI;

public class OpenAIOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4.1-mini";
    public int MaxOutputTokens { get; set; } = 1024;
    public double Temperature { get; set; } = 0.7;
}

public class OpenAIService : HotelAIServiceBase
{
    private const string OPENAI_CHAT_COMPLETIONS_API_URL = "https://api.openai.com/v1/chat/completions";

    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;
    private readonly IAIUsageService _aiUsageService;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(
        HttpClient httpClient,
        IOptions<OpenAIOptions> options,
        IHotelDataService hotelData,
        IAIUsageService aiUsageService,
        ILogger<OpenAIService> logger)
        : base(hotelData)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _aiUsageService = aiUsageService;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");
    }

    public override async Task<string> GetChatCompletionAsync(
        string systemPrompt,
        List<ConversationMessage> history,
        string userMessage,
        string operation = "chat_reply",
        string? hotelId = null,
        string? sessionId = null)
    {
        var messages = new List<object>
        {
            new
            {
                role = "system",
                content = systemPrompt
            }
        };

        messages.AddRange(history.Select(m => new
        {
            role = NormalizeRole(m.Role),
            content = m.Content
        }));

        messages.Add(new
        {
            role = "user",
            content = userMessage
        });

        var requestBody = new
        {
            model = _options.Model,
            messages,
            max_completion_tokens = _options.MaxOutputTokens,
            temperature = _options.Temperature
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(OPENAI_CHAT_COMPLETIONS_API_URL, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, error);
                return "Dạ, em đang gặp sự cố kỹ thuật nhỏ. Anh/chị vui lòng thử lại sau giây lát nhé!";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var assistantText = ExtractAssistantText(responseJson)
                ?? "Em chưa hiểu ý anh/chị, anh/chị có thể nói rõ hơn không ạ? 😊";
            await LogUsageAsync(responseJson, operation, hotelId, sessionId, systemPrompt, history, userMessage, assistantText);
            return assistantText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API");
            return "Dạ, em đang gặp sự cố kết nối. Anh/chị vui lòng thử lại sau ít phút nhé! 🙏";
        }
    }

    private static string NormalizeRole(string role)
        => role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";

    private async Task LogUsageAsync(
        string responseJson,
        string operation,
        string? hotelId,
        string? sessionId,
        string systemPrompt,
        List<ConversationMessage> history,
        string userMessage,
        string assistantText)
    {
        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("usage", out var usage))
            return;

        var promptTokens = GetInt32OrDefault(usage, "prompt_tokens");
        var completionTokens = GetInt32OrDefault(usage, "completion_tokens");
        var totalTokens = GetInt32OrDefault(usage, "total_tokens");
        var historyChars = history.Sum(x => x.Content?.Length ?? 0);

        await _aiUsageService.TrackUsageAsync(new HotelChatbot.Core.Models.AiUsageLog
        {
            TimestampUtc = DateTime.UtcNow,
            HotelId = hotelId ?? "",
            SessionId = sessionId ?? "",
            Provider = "OpenAI",
            Model = _options.Model,
            Operation = operation,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = totalTokens,
            SystemPromptChars = systemPrompt.Length,
            HistoryChars = historyChars,
            UserMessageChars = userMessage.Length,
            InputTextChars = systemPrompt.Length + historyChars + userMessage.Length,
            OutputChars = assistantText.Length
        });

        _logger.LogInformation(
            "OpenAI usage tracked: operation={Operation}, hotelId={HotelId}, sessionId={SessionId}, model={Model}, prompt_tokens={PromptTokens}, completion_tokens={CompletionTokens}, total_tokens={TotalTokens}",
            operation,
            hotelId ?? "",
            sessionId ?? "",
            _options.Model,
            promptTokens,
            completionTokens,
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

    private static string? ExtractAssistantText(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);

        if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }
        }

        return null;
    }
}
