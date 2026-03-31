using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelChatbot.Infrastructure.Services;

public class ZaloWebhookService : IZaloWebhookService
{
    private const string ZaloSendApiUrl = "https://openapi.zalo.me/v3.0/oa/message/cs";
    private const int DuplicateMessageCacheMinutes = 10;
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ZaloOptions _options;
    private readonly ILogger<ZaloWebhookService> _logger;
    private readonly ConcurrentDictionary<string, PendingMessageBuffer> _pendingBuffers = new();

    public ZaloWebhookService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<ZaloOptions> options,
        ILogger<ZaloWebhookService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool VerifySignature(string payload, string? signatureHeader)
    {
        // Zalo gửi header X-ZaloOA-Signature: sha256=HMAC(SECRET_KEY, body)
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(_options.DefaultSecretKey))
            return true; // Bỏ qua verify nếu chưa cấu hình secret (dev mode)

        var expectedPrefix = "sha256=";
        if (!signatureHeader.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var receivedHash = signatureHeader[expectedPrefix.Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.DefaultSecretKey));
        var computedHash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        return string.Equals(computedHash, receivedHash, StringComparison.OrdinalIgnoreCase);
    }

    public Task HandleWebhookAsync(string payloadJson)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(payloadJson))
            return Task.CompletedTask;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var eventName = root.TryGetProperty("event_name", out var evtEl) ? evtEl.GetString() ?? "" : "";
            if (!string.Equals(eventName, "user_send_text", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Zalo webhook ignored event_name={EventName}", eventName);
                return Task.CompletedTask;
            }

            var oaId = root.TryGetProperty("app_id", out var oaEl) ? oaEl.GetString() ?? "" : "";
            var userId = root.TryGetProperty("user_id", out var userEl) ? userEl.GetString() ?? "" : "";
            var timestamp = root.TryGetProperty("timestamp", out var tsEl) && tsEl.TryGetInt64(out var ts) ? ts : 0;

            string text = "";
            string msgId = "";
            if (root.TryGetProperty("message", out var msgEl))
            {
                text = msgEl.TryGetProperty("text", out var textEl) ? textEl.GetString() ?? "" : "";
                msgId = msgEl.TryGetProperty("msg_id", out var midEl) ? midEl.GetString() ?? "" : "";
            }

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(text))
                return Task.CompletedTask;

            var evt = new ZaloMessageEvent
            {
                OaId = oaId,
                UserId = userId,
                MessageId = msgId,
                Text = text,
                TimestampMs = timestamp
            };

            _logger.LogInformation(
                "Zalo webhook received: oaId={OaId}, userId={UserId}, msgId={MsgId}, text={Text}",
                oaId, userId, msgId, text);

            EnqueueDebouncedMessage(evt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Zalo webhook payload");
        }

        return Task.CompletedTask;
    }

    private void EnqueueDebouncedMessage(ZaloMessageEvent evt)
    {
        var key = $"{evt.OaId}:{evt.UserId}";
        var buffer = _pendingBuffers.GetOrAdd(key, _ => new PendingMessageBuffer());
        CancellationTokenSource delayTokenSource;

        lock (buffer.SyncRoot)
        {
            buffer.Events.Add(evt);
            buffer.DelayTokenSource?.Cancel();
            buffer.DelayTokenSource?.Dispose();
            delayTokenSource = new CancellationTokenSource();
            buffer.DelayTokenSource = delayTokenSource;
        }

        _ = Task.Run(() => ProcessBufferedMessagesAsync(key, buffer, delayTokenSource));
    }

    private async Task ProcessBufferedMessagesAsync(
        string conversationKey,
        PendingMessageBuffer buffer,
        CancellationTokenSource delayTokenSource)
    {
        try
        {
            await Task.Delay(DebounceWindow, delayTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        List<ZaloMessageEvent> bufferedEvents;
        lock (buffer.SyncRoot)
        {
            if (!ReferenceEquals(buffer.DelayTokenSource, delayTokenSource))
                return;

            bufferedEvents = buffer.Events.OrderBy(e => e.TimestampMs).ToList();
            buffer.Events.Clear();
            buffer.DelayTokenSource = null;
        }

        _pendingBuffers.TryRemove(conversationKey, out _);
        delayTokenSource.Dispose();

        if (bufferedEvents.Count == 0) return;

        var latestEvent = bufferedEvents[^1];
        var mergedText = string.Join("\n", bufferedEvents
            .Select(e => e.Text?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        if (string.IsNullOrWhiteSpace(mergedText)) return;

        await TrySendChatbotReplyAsync(latestEvent, mergedText);
    }

    private async Task TrySendChatbotReplyAsync(ZaloMessageEvent evt, string mergedText)
    {
        var accessToken = ResolveAccessToken(evt.OaId);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("Zalo AccessToken is empty for oaId={OaId}. Skip sending reply for userId={UserId}", evt.OaId, evt.UserId);
            return;
        }

        string reply;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();

            var chatResponse = await conversationService.ProcessMessageAsync(new ChatRequest
            {
                SessionId = $"zalo:{evt.OaId}:{evt.UserId}",
                Message = mergedText,
                HotelId = ResolveHotelId(evt.OaId),
                Language = "vi"
            });

            reply = string.IsNullOrWhiteSpace(chatResponse.Message)
                ? "Dạ em đã nhận được tin nhắn của anh/chị rồi ạ."
                : chatResponse.Message.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Zalo message for userId={UserId}", evt.UserId);
            reply = "Dạ em đã nhận được tin nhắn của anh/chị rồi ạ.";
        }

        var requestBody = new
        {
            recipient = new { user_id = evt.UserId },
            message = new { text = reply }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("access_token", accessToken);

        try
        {
            var response = await httpClient.PostAsync(ZaloSendApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Zalo Send API error: status={StatusCode}, userId={UserId}, body={Body}",
                    response.StatusCode, evt.UserId, responseBody);
                return;
            }

            _logger.LogInformation(
                "Zalo auto-reply sent: oaId={OaId}, userId={UserId}, reply={Reply}",
                evt.OaId, evt.UserId, reply);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Zalo reply to userId={UserId}", evt.UserId);
        }
    }

    private string ResolveHotelId(string oaId)
    {
        if (!string.IsNullOrEmpty(oaId) && _options.OaHotelMapping.TryGetValue(oaId, out var hotelId))
            return hotelId;
        return _options.DefaultHotelId;
    }

    private string ResolveAccessToken(string oaId)
    {
        if (!string.IsNullOrEmpty(oaId) && _options.OaAccessTokenMapping.TryGetValue(oaId, out var token))
            return token;
        return _options.DefaultAccessToken;
    }

    private sealed class PendingMessageBuffer
    {
        public object SyncRoot { get; } = new();
        public List<ZaloMessageEvent> Events { get; } = new();
        public CancellationTokenSource? DelayTokenSource { get; set; }
    }

    private sealed class ZaloMessageEvent
    {
        public string OaId { get; set; } = "";
        public string UserId { get; set; } = "";
        public string MessageId { get; set; } = "";
        public string Text { get; set; } = "";
        public long TimestampMs { get; set; }
    }
}
