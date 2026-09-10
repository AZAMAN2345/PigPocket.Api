using System.ComponentModel.DataAnnotations;
using PigPocket.Api.Models.Enums;

namespace PigPocket.Api.DTOs.Transactions;

public class CreateTransactionDto
{
    public string? Merchant { get; set; }
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    public TransactionType Type { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime? TransactionDate { get; set; }
}
