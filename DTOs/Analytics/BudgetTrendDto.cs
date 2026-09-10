namespace PigPocket.Api.DTOs.Analytics;

public class BudgetTrendPointDto
{
    public string Label { get; set; } = "";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining { get; set; }
    public decimal PercentageUsed { get; set; }
    public bool WasOverBudget { get; set; }
}

public class BudgetTrendDto
{
    public string Range { get; set; } = "";
    public List<BudgetTrendPointDto> Points { get; set; } = [];
}
