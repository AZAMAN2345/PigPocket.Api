using PigPocket.Api.Models.Enums;

namespace PigPocket.Api.Models;

public class BudgetPlan
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public BudgetPeriod Period { get; set; }
    public BudgetInputMode InputMode { get; set; }
    public decimal TotalBudgetAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<BudgetAllocation> Allocations { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
