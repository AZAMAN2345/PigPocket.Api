namespace PigPocket.Api.DTOs.Budgets;

public class BudgetAllocationDto
{
    public Guid CategoryId { get; set; }
    public decimal? Percentage { get; set; }
    public decimal? Amount { get; set; }
}
