namespace PigPocket.Api.DTOs.Budgets;

public class CategoryBudgetStatusDto
{
    public Guid CategoryId { get; set; }
    public string Category { get; set; } = "";
    public decimal AllocationPercentage { get; set; }
    public decimal Budget { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining { get; set; }
    public decimal PercentageUsed { get; set; }
    public bool IsOverBudget { get; set; }
    public decimal OverBudgetAmount { get; set; }
}
