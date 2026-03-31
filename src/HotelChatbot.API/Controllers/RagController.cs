using HotelChatbot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HotelChatbot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RagController : ControllerBase
{
    private readonly IKnowledgeIndexingService _indexingService;
    private readonly IRagContextService _ragContextService;

    public RagController(
        IKnowledgeIndexingService indexingService,
        IRagContextService ragContextService)
    {
        _indexingService = indexingService;
        _ragContextService = ragContextService;
    }

    [HttpPost("reindex/{hotelId}")]
    public async Task<IActionResult> ReindexHotel(string hotelId)
    {
        var count = await _indexingService.ReindexHotelAsync(hotelId);
        return Ok(new
        {
            hotelId,
            indexedChunks = count,
            indexedAt = DateTime.UtcNow
        });
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] RagSearchRequest request)
    {
        var context = await _ragContextService.BuildKnowledgeContextAsync(
            request.HotelId,
            request.Query,
            request.Intent);

        return Ok(new
        {
            request.HotelId,
            request.Query,
            request.Intent,
            context
        });
    }
}

public class RagSearchRequest
{
    public string HotelId { get; set; } = "";
    public string Query { get; set; } = "";
    public string? Intent { get; set; }
}
