using HotelChatbot.Core.DTOs;
using HotelChatbot.Core.Interfaces;
using HotelChatbot.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HotelChatbot.Infrastructure.Services;

public class AIUsageService : IAIUsageService
{
    private readonly IGoogleSheetsService _googleSheetsService;
    private readonly AIUsageOptions _options;
    private readonly ILogger<AIUsageService> _logger;

    public AIUsageService(
        IGoogleSheetsService googleSheetsService,
        IOptions<AIUsageOptions> options,
        ILogger<AIUsageService> logger)
    {
        _googleSheetsService = googleSheetsService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task TrackUsageAsync(AiUsageLog log)
    {
        log.LogId = string.IsNullOrWhiteSpace(log.LogId) ? Guid.NewGuid().ToString() : log.LogId;
        log.TimestampUtc = log.TimestampUtc == default ? DateTime.UtcNow : log.TimestampUtc;
        log.EstimatedCostUsd = CalculateCostUsd(log.Model, log.PromptTokens, log.CompletionTokens, log.Operation);
        log.EstimatedCostVnd = Math.Round(log.EstimatedCostUsd * _options.VndPerUsd, 2);

        await _googleSheetsService.AppendAiUsageLogAsync(log);

        _logger.LogInformation(
            "AI usage persisted: logId={LogId}, hotelId={HotelId}, sessionId={SessionId}, operation={Operation}, model={Model}, total_tokens={TotalTokens}, cost_usd={CostUsd}, cost_vnd={CostVnd}",
            log.LogId,
            log.HotelId,
            log.SessionId,
            log.Operation,
            log.Model,
            log.TotalTokens,
            log.EstimatedCostUsd,
            log.EstimatedCostVnd);
    }

    public async Task<AiUsageSummaryResponse> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, string? hotelId = null)
    {
        var logs = await _googleSheetsService.GetAiUsageLogsAsync(fromUtc, toUtc, hotelId);
        var totals = BuildAggregate(logs);
        var userMessageCount = Math.Max(1, totals.UserMessagesCount);

        var chatReplyLogs = logs.Where(x => x.Operation == "chat_reply").ToList();
        var intentLogs = logs.Where(x => x.Operation == "intent_analysis").ToList();
        var embeddingLogs = logs.Where(x => x.Operation == "embedding_query").ToList();

        return new AiUsageSummaryResponse
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            VndPerUsd = _options.VndPerUsd,
            Totals = totals,
            AveragePerUserMessage = new AiUsageMessageEstimateDto
            {
                AvgCostUsdAllOperations = SafeDivide(totals.CostUsd, userMessageCount),
                AvgCostVndAllOperations = SafeDivide(totals.CostVnd, userMessageCount),
                AvgPromptTokensAllOperations = SafeDivide(totals.PromptTokens, userMessageCount),
                AvgCompletionTokensAllOperations = SafeDivide(totals.CompletionTokens, userMessageCount),
                AvgTotalTokensAllOperations = SafeDivide(totals.TotalTokens, userMessageCount),
                AvgChatReplyPromptTokens = SafeDivide(chatReplyLogs.Sum(x => x.PromptTokens), userMessageCount),
                AvgChatReplyCompletionTokens = SafeDivide(chatReplyLogs.Sum(x => x.CompletionTokens), userMessageCount),
                AvgChatReplyTotalTokens = SafeDivide(chatReplyLogs.Sum(x => x.TotalTokens), userMessageCount),
                AvgIntentPromptTokens = SafeDivide(intentLogs.Sum(x => x.PromptTokens), userMessageCount),
                AvgIntentCompletionTokens = SafeDivide(intentLogs.Sum(x => x.CompletionTokens), userMessageCount),
                AvgEmbeddingPromptTokens = SafeDivide(embeddingLogs.Sum(x => x.PromptTokens), userMessageCount),
                AvgSystemPromptChars = SafeDivide(chatReplyLogs.Sum(x => x.SystemPromptChars), userMessageCount),
                AvgHistoryChars = SafeDivide(chatReplyLogs.Sum(x => x.HistoryChars), userMessageCount),
                AvgUserMessageChars = SafeDivide(chatReplyLogs.Sum(x => x.UserMessageChars), userMessageCount),
                AvgOutputChars = SafeDivide(chatReplyLogs.Sum(x => x.OutputChars), userMessageCount)
            },
            ByDay = logs
                .GroupBy(x => x.TimestampUtc.Date)
                .OrderBy(x => x.Key)
                .Select(g =>
                {
                    var aggregate = BuildAggregate(g.ToList());
                    return new AiUsageAggregateByDateDto
                    {
                        DateUtc = g.Key,
                        LogsCount = aggregate.LogsCount,
                        UserMessagesCount = aggregate.UserMessagesCount,
                        SessionsCount = aggregate.SessionsCount,
                        PromptTokens = aggregate.PromptTokens,
                        CompletionTokens = aggregate.CompletionTokens,
                        TotalTokens = aggregate.TotalTokens,
                        CostUsd = aggregate.CostUsd,
                        CostVnd = aggregate.CostVnd
                    };
                })
                .ToList(),
            ByHotel = logs
                .GroupBy(x => string.IsNullOrWhiteSpace(x.HotelId) ? "unknown" : x.HotelId)
                .OrderBy(x => x.Key)
                .Select(g => BuildAggregateByKey(g.Key, g.ToList()))
                .ToList(),
            ByModel = logs
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Model) ? "unknown" : x.Model)
                .OrderBy(x => x.Key)
                .Select(g => BuildAggregateByKey(g.Key, g.ToList()))
                .ToList(),
            ByOperation = logs
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Operation) ? "unknown" : x.Operation)
                .OrderBy(x => x.Key)
                .Select(g => BuildAggregateByKey(g.Key, g.ToList()))
                .ToList()
        };
    }

    private AiUsageAggregateByKeyDto BuildAggregateByKey(string key, List<AiUsageLog> logs)
    {
        var aggregate = BuildAggregate(logs);
        return new AiUsageAggregateByKeyDto
        {
            Key = key,
            LogsCount = aggregate.LogsCount,
            UserMessagesCount = aggregate.UserMessagesCount,
            SessionsCount = aggregate.SessionsCount,
            PromptTokens = aggregate.PromptTokens,
            CompletionTokens = aggregate.CompletionTokens,
            TotalTokens = aggregate.TotalTokens,
            CostUsd = aggregate.CostUsd,
            CostVnd = aggregate.CostVnd
        };
    }

    private static AiUsageAggregateDto BuildAggregate(List<AiUsageLog> logs)
    {
        return new AiUsageAggregateDto
        {
            LogsCount = logs.Count,
            UserMessagesCount = logs.Count(x => x.Operation == "chat_reply"),
            SessionsCount = logs
                .Where(x => !string.IsNullOrWhiteSpace(x.SessionId))
                .Select(x => x.SessionId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            PromptTokens = logs.Sum(x => (long)x.PromptTokens),
            CompletionTokens = logs.Sum(x => (long)x.CompletionTokens),
            TotalTokens = logs.Sum(x => (long)x.TotalTokens),
            CostUsd = logs.Sum(x => x.EstimatedCostUsd),
            CostVnd = logs.Sum(x => x.EstimatedCostVnd)
        };
    }

    private static decimal CalculateCostUsd(string model, int promptTokens, int completionTokens, string operation)
    {
        var normalizedModel = model?.Trim().ToLowerInvariant() ?? "";

        if (normalizedModel == "gpt-4.1-mini")
            return Calculate(promptTokens, completionTokens, 0.40m, 1.60m);

        if (normalizedModel == "gpt-4o-mini")
            return Calculate(promptTokens, completionTokens, 0.15m, 0.60m);

        if (normalizedModel == "text-embedding-3-small" || operation.StartsWith("embedding_", StringComparison.OrdinalIgnoreCase))
            return Calculate(promptTokens, 0, 0.02m, 0m);

        return 0m;
    }

    private static decimal Calculate(int promptTokens, int completionTokens, decimal inputRatePerMillion, decimal outputRatePerMillion)
    {
        var inputCost = (promptTokens / 1_000_000m) * inputRatePerMillion;
        var outputCost = (completionTokens / 1_000_000m) * outputRatePerMillion;
        return Math.Round(inputCost + outputCost, 8, MidpointRounding.AwayFromZero);
    }

    private static decimal SafeDivide(decimal value, int divisor)
        => divisor <= 0 ? 0 : Math.Round(value / divisor, 2);

    private static decimal SafeDivide(long value, int divisor)
        => divisor <= 0 ? 0 : Math.Round(value / (decimal)divisor, 2);
}
