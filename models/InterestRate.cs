namespace PigPocket.Api.Models;

public class InterestRate
{
    public Guid Id { get; set; }
    public Guid SavingsAccountId { get; set; }
    public decimal AnnualRate { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
}
