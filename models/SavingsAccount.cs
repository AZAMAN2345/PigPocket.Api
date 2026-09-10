namespace PigPocket.Api.Models;

public class SavingsAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public decimal Principal { get; set; }
    public decimal AccruedInterest { get; set; }
    public string Currency { get; set; } = "NGN";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
