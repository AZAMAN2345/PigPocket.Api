namespace PigPocket.Api.Models;

public class BankAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BankName { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountNumberMasked { get; set; } = "";
    public string Provider { get; set; } = "";
    public string? ProviderAccountId { get; set; }
    public decimal CurrentBalance { get; set; }
    public string Currency { get; set; } = "NGN";
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
