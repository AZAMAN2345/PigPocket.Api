namespace PigPocket.Api.Models;

public class BankConnectionSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Reference { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ProviderAccountId { get; set; }
}
