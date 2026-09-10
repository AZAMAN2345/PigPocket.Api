using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.Configuration;
using PigPocket.Api.Services;

namespace PigPocket.Api.Controllers;

[ApiController]
[Route("api/webhooks/mono")]
public class MonoWebhookController : ControllerBase
{
    private readonly BankingService _bankingService;
    private readonly MonoSettings _monoSettings;

    public MonoWebhookController(
        BankingService bankingService,
        MonoSettings monoSettings)
    {
        _bankingService = bankingService;
        _monoSettings = monoSettings;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_monoSettings.WebhookSecret))
        {
            return StatusCode(500, new { message = "Mono webhook secret is not configured." });
        }

        if (!Request.Headers.TryGetValue("mono-webhook-secret", out var receivedSecret)
            || receivedSecret != _monoSettings.WebhookSecret)
        {
            return Unauthorized(new { message = "Unauthorized request." });
        }

        var result = await _bankingService.ProcessMonoWebhookAsync(
            payload,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.Error switch
        {
            BankingError.BadRequest => BadRequest(new { message = result.ErrorMessage }),
            BankingError.NotFound => NotFound(new { message = result.ErrorMessage }),
            BankingError.ProviderUnavailable => StatusCode(502, new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }
}
