using HotelChatbot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HotelChatbot.API.Controllers;

[ApiController]
[Route("api/messenger/webhook")]
[Produces("application/json")]
public class MessengerController : ControllerBase
{
    private readonly IMessengerWebhookService _messengerWebhookService;
    private readonly ILogger<MessengerController> _logger;

    public MessengerController(
        IMessengerWebhookService messengerWebhookService,
        ILogger<MessengerController> logger)
    {
        _messengerWebhookService = messengerWebhookService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (_messengerWebhookService.ValidateWebhookSubscription(mode, verifyToken, out var verifiedChallenge, challenge))
            return Content(verifiedChallenge, "text/plain");

        _logger.LogWarning("Messenger webhook verification failed: mode={Mode}", mode);
        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        await _messengerWebhookService.HandleWebhookAsync(payload);
        return Ok("EVENT_RECEIVED");
    }
}
