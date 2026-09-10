using PigPocket.Api.Models.Enums;

namespace PigPocket.Api.Models;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? BankAccountId { get; set; }
    public string Merchant { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public Guid? CategoryId { get; set; }
    public TransactionSource Source { get; set; }
    public DateTime TransactionDate { get; set; }
    public bool IsInternalTransfer { get; set; }
    public bool CountsAsSpending { get; set; } = true;
    public decimal? CategoryConfidence { get; set; }
    public string? ProviderTransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
