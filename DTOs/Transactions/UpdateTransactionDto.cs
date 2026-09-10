namespace PigPocket.Api.DTOs.Transactions;

public class UpdateTransactionDto
{
    public string? Merchant { get; set; }
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime? TransactionDate { get; set; }
}
