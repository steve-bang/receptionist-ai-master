using HotelChatbot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HotelChatbot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsageController : ControllerBase
{
    private readonly IAIUsageService _aiUsageService;

    public UsageController(IAIUsageService aiUsageService)
    {
        _aiUsageService = aiUsageService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? hotelId = null)
    {
        var fromUtc = (from ?? DateTime.UtcNow.Date.AddDays(-30)).ToUniversalTime();
        var toUtc = (to ?? DateTime.UtcNow).ToUniversalTime();

        if (fromUtc > toUtc)
            return BadRequest(new { error = "Thời gian 'from' phải nhỏ hơn hoặc bằng 'to'" });

        var summary = await _aiUsageService.GetSummaryAsync(fromUtc, toUtc, hotelId);
        return Ok(summary);
    }
}
