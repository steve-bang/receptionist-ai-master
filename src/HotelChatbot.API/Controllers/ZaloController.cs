using HotelChatbot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HotelChatbot.API.Controllers;

[ApiController]
[Route("api/zalo/webhook")]
[Produces("application/json")]
public class ZaloController : ControllerBase
{
    private readonly IZaloWebhookService _zaloWebhookService;
    private readonly ILogger<ZaloController> _logger;

    public ZaloController(IZaloWebhookService zaloWebhookService, ILogger<ZaloController> logger)
    {
        _zaloWebhookService = zaloWebhookService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        // Verify signature từ Zalo (header X-ZaloOA-Signature)
        var signature = Request.Headers["X-ZaloOA-Signature"].FirstOrDefault();
        if (!_zaloWebhookService.VerifySignature(payload, signature))
        {
            _logger.LogWarning("Zalo webhook signature verification failed");
            return Unauthorized();
        }

        await _zaloWebhookService.HandleWebhookAsync(payload);
        return Ok();
    }
}
