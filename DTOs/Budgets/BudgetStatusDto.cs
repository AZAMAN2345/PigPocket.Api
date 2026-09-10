using PigPocket.Api.Models.Enums;

namespace PigPocket.Api.DTOs.Budgets;

public class BudgetStatusDto
{
    public Guid BudgetId { get; set; }
    public string Name { get; set; } = "";
    public BudgetPeriod Period { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal Remaining { get; set; }
    public decimal PercentageUsed { get; set; }
    public bool IsOverBudget { get; set; }
    public decimal OverBudgetAmount { get; set; }
    public List<CategoryBudgetStatusDto> Allocations { get; set; } = [];
}
