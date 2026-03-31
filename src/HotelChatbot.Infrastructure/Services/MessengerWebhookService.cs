using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace HotelChatbot.Infrastructure.Services;

public class MessengerWebhookService : IMessengerWebhookService
{
    private const string MessengerSendApiUrl = "https://graph.facebook.com/v23.0/me/messages";
    private const int DuplicateMessageCacheMinutes = 10;
    private const int DuplicateTextFingerprintSeconds = 15;
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly MessengerOptions _options;
    private readonly ILogger<MessengerWebhookService> _logger;
    private readonly ConcurrentDictionary<string, PendingMessageBuffer> _pendingBuffers = new();

    public MessengerWebhookService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        IOptions<MessengerOptions> options,
        ILogger<MessengerWebhookService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _options = options.Value;
        _logger = logger;
    }

    public bool ValidateWebhookSubscription(string? mode, string? verifyToken, out string challenge, string? hubChallenge = null)
    {
        challenge = hubChallenge ?? "";

        return _options.Enabled
            && string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_options.VerifyToken)
            && string.Equals(verifyToken, _options.VerifyToken, StringComparison.Ordinal);
    }

    public Task HandleWebhookAsync(string payloadJson)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(payloadJson))
            return Task.CompletedTask;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("object", out var objectElement) ||
                !string.Equals(objectElement.GetString(), "page", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Messenger webhook ignored because object != page");
                return Task.CompletedTask;
            }

            if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return Task.CompletedTask;

            foreach (var entry in entries.EnumerateArray())
            {
                var pageId = entry.TryGetProperty("id", out var pageIdElement)
                    ? pageIdElement.GetString() ?? ""
                    : "";

                if (!entry.TryGetProperty("messaging", out var messagingArray) || messagingArray.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var messaging in messagingArray.EnumerateArray())
                {
                    var evt = ParseMessagingEvent(pageId, messaging);
                    if (evt == null) continue;

                    if (!ShouldProcessIncomingText(evt))
                        continue;

                    if (!TryMarkEventAsNew(evt))
                    {
                        _logger.LogInformation(
                            "Messenger duplicate event skipped: pageId={PageId}, senderId={SenderId}, recipientId={RecipientId}, messageId={MessageId}, text={Text}",
                            evt.PageId,
                            evt.SenderId,
                            evt.RecipientId,
                            evt.MessageId,
                            evt.Text);
                        continue;
                    }

                    _logger.LogInformation(
                        "Messenger webhook received: pageId={PageId}, senderId={SenderId}, recipientId={RecipientId}, messageId={MessageId}, isEcho={IsEcho}, text={Text}",
                        evt.PageId,
                        evt.SenderId,
                        evt.RecipientId,
                        evt.MessageId,
                        evt.IsEcho,
                        evt.Text);

                    EnqueueDebouncedMessage(evt);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Messenger webhook payload");
        }

        return Task.CompletedTask;
    }

    private void EnqueueDebouncedMessage(MessengerWebhookEventDto evt)
    {
        var conversationKey = GetConversationKey(evt);
        var buffer = _pendingBuffers.GetOrAdd(conversationKey, _ => new PendingMessageBuffer());
        CancellationTokenSource delayTokenSource;
        int pendingCount;

        lock (buffer.SyncRoot)
        {
            buffer.Events.Add(evt);
            buffer.DelayTokenSource?.Cancel();
            buffer.DelayTokenSource?.Dispose();

            delayTokenSource = new CancellationTokenSource();
            buffer.DelayTokenSource = delayTokenSource;
            pendingCount = buffer.Events.Count;
        }

        _logger.LogInformation(
            "Messenger message buffered: conversationKey={ConversationKey}, pendingCount={PendingCount}, debounceSeconds={DebounceSeconds}",
            conversationKey,
            pendingCount,
            DebounceWindow.TotalSeconds);

        _ = Task.Run(() => ProcessBufferedMessagesAsync(conversationKey, buffer, delayTokenSource));
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

        List<MessengerWebhookEventDto> bufferedEvents;
        lock (buffer.SyncRoot)
        {
            if (!ReferenceEquals(buffer.DelayTokenSource, delayTokenSource))
                return;

            bufferedEvents = buffer.Events
                .OrderBy(e => e.TimestampMs)
                .ToList();

            buffer.Events.Clear();
            buffer.DelayTokenSource = null;
        }

        _pendingBuffers.TryRemove(conversationKey, out _);
        delayTokenSource.Dispose();

        if (bufferedEvents.Count == 0)
            return;

        var latestEvent = bufferedEvents[^1];
        var mergedText = string.Join("\n", bufferedEvents
            .Select(e => e.Text?.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(mergedText))
            return;

        _logger.LogInformation(
            "Messenger debounce window closed: conversationKey={ConversationKey}, bufferedMessages={BufferedMessages}, mergedText={MergedText}",
            conversationKey,
            bufferedEvents.Count,
            mergedText);

        await TrySendChatbotReplyAsync(latestEvent, mergedText, bufferedEvents.Count);
    }

    private async Task TrySendChatbotReplyAsync(
        MessengerWebhookEventDto evt,
        string mergedText,
        int bufferedMessagesCount)
    {
        if (string.IsNullOrWhiteSpace(_options.PageAccessToken))
        {
            _logger.LogWarning("Messenger PageAccessToken is empty. Skip sending reply for senderId={SenderId}", evt.SenderId);
            return;
        }

        string reply;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();

            var chatResponse = await conversationService.ProcessMessageAsync(new ChatRequest
                {
                    SessionId = evt.SenderId,
                    Message = mergedText,
                    HotelId = _options.DefaultHotelId,
                    Language = "vi"
                });

            reply = string.IsNullOrWhiteSpace(chatResponse.Message)
                ? "Dạ em đã nhận được tin nhắn của anh/chị rồi ạ."
                : chatResponse.Message.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Messenger message with chatbot service for senderId={SenderId}", evt.SenderId);
            reply = "Dạ em đã nhận được tin nhắn của anh/chị rồi ạ.";
        }

        var requestBody = new
        {
            recipient = new
            {
                id = evt.SenderId
            },
            messaging_type = "RESPONSE",
            message = new
            {
                text = reply
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var requestUrl = $"{MessengerSendApiUrl}?access_token={Uri.EscapeDataString(_options.PageAccessToken)}";
        var httpClient = _httpClientFactory.CreateClient();

        try
        {
            var response = await httpClient.PostAsync(requestUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Messenger Send API error: status={StatusCode}, senderId={SenderId}, body={Body}",
                    response.StatusCode,
                    evt.SenderId,
                    responseBody);
                return;
            }

            _logger.LogInformation(
                "Messenger auto-reply sent: senderId={SenderId}, messageId={MessageId}, bufferedMessages={BufferedMessages}, reply={Reply}",
                evt.SenderId,
                evt.MessageId,
                bufferedMessagesCount,
                reply);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Messenger auto-reply to senderId={SenderId}", evt.SenderId);
        }
    }

    private bool ShouldProcessIncomingText(MessengerWebhookEventDto evt)
    {
        if (evt.IsEcho)
            return false;

        return !string.IsNullOrWhiteSpace(evt.SenderId)
            && !string.IsNullOrWhiteSpace(evt.Text);
    }

    private bool TryMarkEventAsNew(MessengerWebhookEventDto evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.MessageId))
        {
            var messageKey = $"messenger:message:{evt.PageId}:{evt.MessageId}";
            if (_memoryCache.TryGetValue(messageKey, out _))
                return false;

            _memoryCache.Set(messageKey, true, TimeSpan.FromMinutes(DuplicateMessageCacheMinutes));
        }

        var normalizedText = NormalizeText(evt.Text);
        if (string.IsNullOrWhiteSpace(normalizedText))
            return false;

        var fingerprintKey = $"messenger:fingerprint:{evt.PageId}:{evt.SenderId}:{normalizedText}";
        if (_memoryCache.TryGetValue(fingerprintKey, out _))
            return false;

        _memoryCache.Set(fingerprintKey, true, TimeSpan.FromSeconds(DuplicateTextFingerprintSeconds));
        return true;
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return string.Join(' ', text
            .Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
    }

    private static string GetConversationKey(MessengerWebhookEventDto evt)
    {
        return $"{evt.PageId}:{evt.SenderId}";
    }

    private static MessengerWebhookEventDto? ParseMessagingEvent(string pageId, JsonElement messaging)
    {
        if (!messaging.TryGetProperty("sender", out var sender) ||
            !sender.TryGetProperty("id", out var senderIdElement))
        {
            return null;
        }

        var recipientId = messaging.TryGetProperty("recipient", out var recipient) &&
                          recipient.TryGetProperty("id", out var recipientIdElement)
            ? recipientIdElement.GetString() ?? ""
            : "";

        var timestampMs = messaging.TryGetProperty("timestamp", out var timestampElement) &&
                          timestampElement.TryGetInt64(out var parsedTimestamp)
            ? parsedTimestamp
            : 0;

        var result = new MessengerWebhookEventDto
        {
            PageId = pageId,
            SenderId = senderIdElement.GetString() ?? "",
            RecipientId = recipientId,
            TimestampMs = timestampMs,
            PayloadJson = messaging.GetRawText()
        };

        if (messaging.TryGetProperty("message", out var message))
        {
            result.MessageId = message.TryGetProperty("mid", out var midElement)
                ? midElement.GetString() ?? ""
                : "";

            result.Text = message.TryGetProperty("text", out var textElement)
                ? textElement.GetString() ?? ""
                : "";

            result.IsEcho = message.TryGetProperty("is_echo", out var echoElement) && echoElement.ValueKind == JsonValueKind.True;
        }

        return result;
    }

    private sealed class PendingMessageBuffer
    {
        public object SyncRoot { get; } = new();
        public List<MessengerWebhookEventDto> Events { get; } = new();
        public CancellationTokenSource? DelayTokenSource { get; set; }
    }
}
