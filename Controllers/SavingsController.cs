using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.DTOs.Savings;
using PigPocket.Api.Models.Enums;
using PigPocket.Api.Services;

namespace PigPocket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SavingsController : ControllerBase
{
    private readonly SavingsService _savingsService;

    public SavingsController(SavingsService savingsService)
    {
        _savingsService = savingsService;
    }

    [HttpPut("target")]
    public async Task<IActionResult> UpdateTarget(
        UpdateSavingsTargetDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var user = await _savingsService.UpdateSavingsTargetAsync(
            userId,
            dto.TargetSavingsRate,
            cancellationToken);

        return user is null
            ? NotFound(new { message = "User was not found." })
            : Ok(new { targetSavingsRate = user.TargetSavingsRate });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var parsedPeriod = ParsePeriod(period);
        var summary = await _savingsService.GetSavingsSummaryAsync(
            userId,
            parsedPeriod,
            cancellationToken);

        return summary is null
            ? NotFound(new { message = "User was not found." })
            : Ok(summary);
    }

    [HttpGet("projection")]
    public async Task<IActionResult> GetProjection(
        [FromQuery] int months = 12,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        if (months <= 0 || months > 60)
        {
            return BadRequest(new { message = "Months must be between 1 and 60." });
        }

        var projection = await _savingsService.GetProjectionAsync(
            userId,
            months,
            cancellationToken);

        return projection is null
            ? NotFound(new { message = "User was not found." })
            : Ok(projection);
    }

    private static BudgetPeriod ParsePeriod(string? period)
    {
        return period?.ToLowerInvariant() switch
        {
            "daily" => BudgetPeriod.Daily,
            "weekly" => BudgetPeriod.Weekly,
            _ => BudgetPeriod.Monthly
        };
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
