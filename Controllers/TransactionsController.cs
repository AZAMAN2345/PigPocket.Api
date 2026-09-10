using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.DTOs.Transactions;
using PigPocket.Api.Models.Enums;
using PigPocket.Api.Services;

namespace PigPocket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;

    public TransactionsController(TransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] TransactionType? type,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? bankAccountId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] TransactionSource? source,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var transactions = await _transactionService.GetAllAsync(
            userId,
            new TransactionQuery(type, categoryId, bankAccountId, startDate, endDate, source),
            cancellationToken);

        return Ok(transactions);
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

        var transaction = await _transactionService.GetByIdAsync(
            userId,
            id,
            cancellationToken);

        return transaction is null
            ? NotFound(new { message = "Transaction was not found." })
            : Ok(transaction);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateTransactionDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var result = await _transactionService.CreateManualAsync(
            userId,
            dto,
            cancellationToken);

        if (!result.IsSuccess || result.Value is null)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Transaction could not be created." });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value.Id },
            result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateTransactionDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "Invalid or missing user identity." });
        }

        var transaction = await _transactionService.UpdateAsync(
            userId,
            id,
            dto,
            cancellationToken);

        return transaction is null
            ? NotFound(new { message = "Transaction was not found." })
            : Ok(transaction);
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

        var deleted = await _transactionService.DeleteAsync(
            userId,
            id,
            cancellationToken);

        return deleted
            ? NoContent()
            : NotFound(new { message = "Transaction was not found." });
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}
