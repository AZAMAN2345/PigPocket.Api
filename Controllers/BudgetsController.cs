using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.DTOs.Budgets;
using PigPocket.Api.Models.Enums;
using PigPocket.Api.Services;

namespace PigPocket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BudgetsController : ControllerBase
{
    private readonly BudgetService _budgetService;

    public BudgetsController(BudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateBudgetPlanDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var result = await _budgetService.CreateBudgetAsync(userId, dto, cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        return Ok(await _budgetService.GetBudgetsAsync(userId, cancellationToken));
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(
        [FromQuery] BudgetPeriod? period,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var budget = await _budgetService.GetCurrentBudgetAsync(userId, period, cancellationToken);

        return budget is null
            ? NotFound(new { message = "Current budget was not found." })
            : Ok(budget);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var budget = await _budgetService.GetBudgetByIdAsync(userId, id, cancellationToken);

        return budget is null
            ? NotFound(new { message = "Budget was not found." })
            : Ok(budget);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateBudgetPlanDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var result = await _budgetService.UpdateBudgetAsync(userId, id, dto, cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return result.ErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        return await _budgetService.DeleteBudgetAsync(userId, id, cancellationToken)
            ? NoContent()
            : NotFound(new { message = "Budget was not found." });
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var result = await _budgetService.GetBudgetStatusAsync(userId, id, cancellationToken);

        return result.IsSuccess && result.Value is not null
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
