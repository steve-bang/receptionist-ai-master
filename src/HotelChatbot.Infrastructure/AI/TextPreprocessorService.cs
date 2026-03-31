using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelChatbot.Infrastructure.AI;

public class TextPreprocessorOptions
{
    public bool Enabled { get; set; } = true;
    /// <summary>Model dùng cho preprocessing. Dùng gpt-4o-mini để tối ưu cost + speed.</summary>
    public string Model { get; set; } = "gpt-4o-mini";
    /// <summary>Độ dài tối thiểu (chars) để bật AI preprocessing. Ngắn hơn dùng rule-based.</summary>
    public int MinLengthForAi { get; set; } = 8;
    /// <summary>Max tokens cho preprocessing response. Giữ nhỏ để tối ưu latency.</summary>
    public int MaxTokens { get; set; } = 256;
}

public class TextPreprocessorService : ITextPreprocessorService
{
    private const string OPENAI_CHAT_COMPLETIONS_API_URL = "https://api.openai.com/v1/chat/completions";

    // Regex detect nếu text đã có đủ dấu tiếng Việt — skip AI nếu đúng
    private static readonly Regex _vietDiacriticsRegex = new(
        @"[àáảãạăắặằẳẵâấậầẩẫđèéẻẽẹêếệềểễìíỉĩịòóỏõọôốộồổỗơớợờởỡùúủũụưứựừửữỳýỷỹỵ]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _latinOnlyRegex = new(@"^[a-z0-9\s\.,!?\-/]+$", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly TextPreprocessorOptions _options;
    private readonly OpenAIOptions _openAIOptions;
    private readonly IAIUsageService _aiUsageService;
    private readonly ILogger<TextPreprocessorService> _logger;

    private static readonly string _systemPrompt = """
        You are a Vietnamese hotel chatbot text preprocessor. Your job is to normalize user messages for hotel booking context.

        TASKS:
        1. Restore missing Vietnamese diacritics (e.g. "phong view bien" → "phòng view biển", "dat phong" → "đặt phòng")
        2. Fix common typos in hotel context (e.g. "checkin" → "check-in", "wifi" → "WiFi")
        3. Create an optimized RAG search query: keep hotel-domain keywords, remove filler words
        4. Detect language: "vi" (Vietnamese), "en" (English), "mixed"
        5. Extract hotel-domain key terms

        HOTEL DOMAIN TERMS: phòng, đặt phòng, check-in, check-out, giá, khách sạn, view, biển, suite, deluxe, tiện nghi, bữa sáng, hủy phòng, khuyến mãi

        RULES:
        - If text already has Vietnamese diacritics and no typos → set was_normalized: false
        - If text is English → keep as-is, set language: "en"
        - For rag_query: concise, remove greetings/fillers like "dạ", "ạ", "cho tôi hỏi", "em muốn"
        - Always return valid JSON, nothing else

        OUTPUT FORMAT:
        {
          "normalized": "<corrected text>",
          "rag_query": "<concise hotel search query>",
          "language": "vi|en|mixed",
          "key_terms": ["term1", "term2"],
          "was_normalized": true|false
        }
        """;

    public TextPreprocessorService(
        HttpClient httpClient,
        IOptions<TextPreprocessorOptions> options,
        IOptions<OpenAIOptions> openAIOptions,
        IAIUsageService aiUsageService,
        ILogger<TextPreprocessorService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _openAIOptions = openAIOptions.Value;
        _aiUsageService = aiUsageService;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAIOptions.ApiKey}");
    }

    public async Task<PreprocessedMessage> PreprocessAsync(string text, string? hotelId = null, string? sessionId = null)
    {
        var result = new PreprocessedMessage
        {
            Original = text,
            Normalized = text,
            RagQuery = text,
            Language = "vi",
            KeyTerms = new List<string>(),
            WasNormalized = false,
            Skipped = false
        };

        // Skip: disabled or empty
        if (!_options.Enabled || string.IsNullOrWhiteSpace(text))
        {
            result.Skipped = true;
            return result;
        }

        var normalized = text.Trim();

        // Skip: too short — rule-based clean only
        if (normalized.Length < _options.MinLengthForAi)
        {
            result.Normalized = NormalizeBasic(normalized);
            result.RagQuery = result.Normalized;
            result.Skipped = true;
            return result;
        }

        // Heuristic skip: already has Vietnamese diacritics AND not all-latin
        // → text likely already correct, still extract key terms via AI but mark as optimization candidate
        var hasVietnameseDiacritics = _vietDiacriticsRegex.IsMatch(normalized);
        var isLatinOnly = _latinOnlyRegex.IsMatch(normalized.ToLowerInvariant());

        if (hasVietnameseDiacritics && !isLatinOnly)
        {
            // Text đã có dấu — vẫn gọi AI để extract rag_query và key_terms tốt hơn
            // nhưng đánh dấu was_normalized=false
            _logger.LogDebug("[Preprocessor] Text has diacritics, using AI for rag_query/key_terms only");
        }
        else if (isLatinOnly)
        {
            _logger.LogDebug("[Preprocessor] Latin-only text (possible missing diacritics), full normalization needed");
        }

        return await CallAiPreprocessorAsync(result, normalized, hotelId, sessionId);
    }

    private async Task<PreprocessedMessage> CallAiPreprocessorAsync(
        PreprocessedMessage result, string text, string? hotelId, string? sessionId)
    {
        var requestBody = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = _systemPrompt },
                new { role = "user", content = text }
            },
            max_completion_tokens = _options.MaxTokens,
            temperature = 0.0  // deterministic for normalization
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(OPENAI_CHAT_COMPLETIONS_API_URL, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[Preprocessor] API error {Status}: {Error}, falling back to original text", response.StatusCode, error);
                result.Skipped = true;
                return result;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            await LogUsageAsync(responseJson, hotelId, sessionId, text);

            var assistantContent = ExtractAssistantText(responseJson);
            if (string.IsNullOrWhiteSpace(assistantContent))
            {
                result.Skipped = true;
                return result;
            }

            var parsed = ParsePreprocessorResponse(assistantContent);
            if (parsed == null)
            {
                result.Skipped = true;
                return result;
            }

            result.Normalized = string.IsNullOrWhiteSpace(parsed.Normalized) ? text : parsed.Normalized;
            result.RagQuery = string.IsNullOrWhiteSpace(parsed.RagQuery) ? result.Normalized : parsed.RagQuery;
            result.Language = parsed.Language ?? "vi";
            result.KeyTerms = parsed.KeyTerms ?? new List<string>();
            result.WasNormalized = parsed.WasNormalized;

            if (result.WasNormalized)
            {
                _logger.LogInformation(
                    "[Preprocessor] Normalized: original=\"{Original}\" → normalized=\"{Normalized}\" lang={Lang}",
                    text, result.Normalized, result.Language);
            }
            else
            {
                _logger.LogDebug(
                    "[Preprocessor] Clean text, rag_query=\"{RagQuery}\" key_terms=[{Terms}]",
                    result.RagQuery, string.Join(", ", result.KeyTerms));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Preprocessor] Exception during preprocessing, falling back to original");
            result.Skipped = true;
            return result;
        }
    }

    private static string NormalizeBasic(string text)
    {
        // Rule-based: chỉ clean whitespace, không cần AI
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static PreprocessorResponse? ParsePreprocessorResponse(string json)
    {
        try
        {
            var clean = json.Trim();
            if (clean.StartsWith("```"))
                clean = string.Join('\n', clean.Split('\n').Skip(1).SkipLast(1));

            return JsonSerializer.Deserialize<PreprocessorResponse>(clean, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractAssistantText(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch { return null; }
    }

    private async Task LogUsageAsync(string responseJson, string? hotelId, string? sessionId, string inputText)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("usage", out var usage)) return;

            var promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            var completionTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;

            await _aiUsageService.TrackUsageAsync(new HotelChatbot.Core.Models.AiUsageLog
            {
                TimestampUtc = DateTime.UtcNow,
                HotelId = hotelId ?? "",
                SessionId = sessionId ?? "",
                Provider = "OpenAI",
                Model = _options.Model,
                Operation = "text_preprocess",
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                UserMessageChars = inputText.Length,
                InputTextChars = inputText.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Preprocessor] Failed to log usage");
        }
    }

    private sealed class PreprocessorResponse
    {
        public string? Normalized { get; set; }
        public string? RagQuery { get; set; }
        public string? Language { get; set; }
        public List<string>? KeyTerms { get; set; }
        public bool WasNormalized { get; set; }
    }
}
