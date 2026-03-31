using HotelChatbot.Core.Interfaces;

namespace HotelChatbot.Infrastructure.RAG.Services;

public class NoOpRagContextService : IRagContextService
{
    public Task<string> BuildKnowledgeContextAsync(string hotelId, string query, string? intent = null, string? sessionId = null)
        => Task.FromResult(string.Empty);
}
