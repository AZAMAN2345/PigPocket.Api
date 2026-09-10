using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.Enums;
using PigPocket.Api.Models.Enums;
using PigPocket.Api.Services.Analytics;

namespace PigPocket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsService _analyticsService;
    private readonly TrendService _trendService;
    private readonly CategoryAnalyticsService _categoryAnalyticsService;
    private readonly MerchantAnalyticsService _merchantAnalyticsService;

    public AnalyticsController(
        AnalyticsService analyticsService,
        TrendService trendService,
        CategoryAnalyticsService categoryAnalyticsService,
        MerchantAnalyticsService merchantAnalyticsService)
    {
        _analyticsService = analyticsService;
        _trendService = trendService;
        _categoryAnalyticsService = categoryAnalyticsService;
        _merchantAnalyticsService = merchantAnalyticsService;
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

        if (!TryParsePeriod(period, out var parsedPeriod))
        {
            return BadRequest(new { message = "Period must be daily, weekly, or monthly." });
        }

        return Ok(await _analyticsService.GetSummaryAsync(userId, parsedPeriod, cancellationToken));
    }

    [HttpGet("trends/spending")]
    public async Task<IActionResult> GetSpendingTrend(
        [FromQuery] string? range,
        [FromQuery] Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        if (!TryParseRange(range, out var parsedRange))
        {
            return InvalidRange();
        }

        return Ok(await _trendService.GetSpendingTrendAsync(userId, parsedRange, categoryId, cancellationToken));
    }

    [HttpGet("trends/income")]
    public async Task<IActionResult> GetIncomeTrend(
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        if (!TryParseRange(range, out var parsedRange))
        {
            return InvalidRange();
        }

        return Ok(await _trendService.GetIncomeTrendAsync(userId, parsedRange, cancellationToken));
    }

    [HttpGet("trends/budget")]
    public async Task<IActionResult> GetBudgetTrend(
        [FromQuery] string? range,
        [FromQuery] Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        if (!TryParseRange(range, out var parsedRange))
        {
            return InvalidRange();
        }

        return Ok(await _trendService.GetBudgetTrendAsync(userId, parsedRange, categoryId, cancellationToken));
    }

    [HttpGet("trends/savings")]
    public async Task<IActionResult> GetSavingsTrend(
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        if (!TryParseRange(range, out var parsedRange))
        {
            return InvalidRange();
        }

        return Ok(await _trendService.GetSavingsTrendAsync(userId, parsedRange, cancellationToken));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(
        [FromQuery] string? period,
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        if (!TryParsePeriod(period, out var parsedPeriod))
        {
            return BadRequest(new { message = "Period must be daily, weekly, or monthly." });
        }

        if (!TryParseOptionalRange(range, out var parsedRange))
        {
            return InvalidRange();
        }

        return Ok(await _categoryAnalyticsService.GetCategoryBreakdownAsync(
            userId, parsedPeriod, parsedRange, cancellationToken));
    }

    [HttpGet("categories/{categoryId:guid}")]
    public async Task<IActionResult> GetCategoryDetail(
        Guid categoryId,
        [FromQuery] string? period,
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        if (!TryParsePeriod(period, out var parsedPeriod))
        {
            return BadRequest(new { message = "Period must be daily, weekly, or monthly." });
        }

        if (!TryParseOptionalRange(range, out var parsedRange))
        {
            return InvalidRange();
        }

        var detail = await _categoryAnalyticsService.GetCategoryDetailAsync(
            userId, categoryId, parsedPeriod, parsedRange, cancellationToken);
        return detail is null
            ? NotFound(new { message = "Category was not found." })
            : Ok(detail);
    }

    [HttpGet("merchants")]
    public async Task<IActionResult> GetMerchants(
        [FromQuery] string? period,
        [FromQuery] string? range,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        if (!TryParsePeriod(period, out var parsedPeriod))
        {
            return BadRequest(new { message = "Period must be daily, weekly, or monthly." });
        }

        if (!TryParseOptionalRange(range, out var parsedRange))
        {
            return InvalidRange();
        }

        if (limit is < 1 or > 100)
        {
            return BadRequest(new { message = "Limit must be between 1 and 100." });
        }

        return Ok(await _merchantAnalyticsService.GetMerchantBreakdownAsync(
            userId, parsedPeriod, parsedRange, limit, cancellationToken));
    }

    private BadRequestObjectResult InvalidRange()
        => BadRequest(new { message = "Range must be 7days, 1month, 3months, 6months, or 1year." });

    private static bool TryParsePeriod(string? value, out BudgetPeriod period)
    {
        period = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "monthly" => BudgetPeriod.Monthly,
            "daily" => BudgetPeriod.Daily,
            "weekly" => BudgetPeriod.Weekly,
            _ => (BudgetPeriod)(-1)
        };
        return Enum.IsDefined(period);
    }

    private static bool TryParseOptionalRange(string? value, out TrendRange? range)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            range = null;
            return true;
        }

        var success = TryParseRange(value, out var parsed);
        range = success ? parsed : null;
        return success;
    }

    private static bool TryParseRange(string? value, out TrendRange range)
    {
        range = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "1month" or "onemonth" => TrendRange.OneMonth,
            "7days" or "sevendays" => TrendRange.SevenDays,
            "3months" or "threemonths" => TrendRange.ThreeMonths,
            "6months" or "sixmonths" => TrendRange.SixMonths,
            "1year" or "oneyear" => TrendRange.OneYear,
            _ => (TrendRange)(-1)
        };
        return Enum.IsDefined(range);
    }

    private bool TryGetUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
