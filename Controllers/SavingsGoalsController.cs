using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.DTOs.Goals;
using PigPocket.Api.Services;

namespace PigPocket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/goals")]
public class SavingsGoalsController : ControllerBase
{
    private readonly SavingsGoalService _savingsGoalService;

    public SavingsGoalsController(SavingsGoalService savingsGoalService)
    {
        _savingsGoalService = savingsGoalService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateSavingsGoalDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var goal = await _savingsGoalService.CreateGoalAsync(userId, dto, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = goal.Id }, goal);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        return Ok(await _savingsGoalService.GetGoalsAsync(userId, cancellationToken));
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

        var goal = await _savingsGoalService.GetGoalAsync(userId, id, cancellationToken);

        return goal is null
            ? NotFound(new { message = "Goal was not found." })
            : Ok(goal);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateSavingsGoalDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var goal = await _savingsGoalService.UpdateGoalAsync(userId, id, dto, cancellationToken);

        return goal is null
            ? NotFound(new { message = "Goal was not found." })
            : Ok(goal);
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

        return await _savingsGoalService.DeleteGoalAsync(userId, id, cancellationToken)
            ? NoContent()
            : NotFound(new { message = "Goal was not found." });
    }

    [HttpPost("{id}/contributions")]
    public async Task<IActionResult> AddContribution(
        Guid id,
        AddGoalContributionDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var goal = await _savingsGoalService.AddContributionAsync(userId, id, dto, cancellationToken);

        return goal is null
            ? NotFound(new { message = "Goal was not found." })
            : Ok(goal);
    }

    [HttpGet("{id}/contributions")]
    public async Task<IActionResult> GetContributions(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        return Ok(await _savingsGoalService.GetContributionsAsync(userId, id, cancellationToken));
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
