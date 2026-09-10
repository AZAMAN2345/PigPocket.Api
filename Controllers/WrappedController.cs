using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.Services.Analytics;

namespace PigPocket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WrappedController : ControllerBase
{
    private readonly WrappedService _wrappedService;

    public WrappedController(WrappedService wrappedService)
    {
        _wrappedService = wrappedService;
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> GetMonthly(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var now = DateTime.UtcNow;
        var selectedYear = year ?? now.Year;
        var selectedMonth = month ?? now.Month;
        if (selectedYear is < 2 or > 9998 || selectedMonth is < 1 or > 12)
        {
            return BadRequest(new { message = "A valid year and month are required." });
        }

        return Ok(await _wrappedService.GetMonthlyWrappedAsync(
            userId, selectedYear, selectedMonth, cancellationToken));
    }

    [HttpGet("yearly")]
    public async Task<IActionResult> GetYearly(
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var selectedYear = year ?? DateTime.UtcNow.Year;
        if (selectedYear is < 1 or > 9998)
        {
            return BadRequest(new { message = "A valid year is required." });
        }

        return Ok(await _wrappedService.GetYearlyWrappedAsync(userId, selectedYear, cancellationToken));
    }

    private bool TryGetUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
