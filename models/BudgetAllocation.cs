namespace PigPocket.Api.Models;

public class BudgetAllocation
{
    public Guid CategoryId { get; set; }
    public decimal Percentage { get; set; }
    public decimal AllocatedAmount { get; set; }
}
