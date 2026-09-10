using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.Services;

namespace PigPocket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BankingController : ControllerBase
{
    private readonly BankingService _bankingService;

    public BankingController(BankingService bankingService)
    {
        _bankingService = bankingService;
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var accounts = await _bankingService.GetAccountsAsync(userId, cancellationToken);

        return Ok(accounts);
    }

    [HttpGet("accounts/{id}")]
    public async Task<IActionResult> GetAccount(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var account = await _bankingService.GetAccountAsync(
            userId,
            id,
            cancellationToken);

        return account is null
            ? NotFound(new { message = "Bank account was not found." })
            : Ok(account);
    }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var result = await _bankingService.InitiateConnectionAsync(
            userId,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("accounts/{id}/sync")]
    public async Task<IActionResult> Sync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var result = await _bankingService.SyncAccountAsync(
            userId,
            id,
            cancellationToken);

        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(BankingResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return result.Error switch
        {
            BankingError.BadRequest => BadRequest(new { message = result.ErrorMessage }),
            BankingError.Unauthorized => Unauthorized(new { message = result.ErrorMessage }),
            BankingError.NotFound => NotFound(new { message = result.ErrorMessage }),
            BankingError.Conflict => Conflict(new { message = result.ErrorMessage }),
            BankingError.ProviderUnavailable => StatusCode(502, new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}
