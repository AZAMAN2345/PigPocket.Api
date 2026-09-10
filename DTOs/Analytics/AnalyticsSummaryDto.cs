namespace PigPocket.Api.DTOs.Analytics;

public class AnalyticsSummaryDto
{
    public decimal Income { get; set; }
    public decimal? IncomeChangePercentage { get; set; }
    public decimal Spending { get; set; }
    public decimal? SpendingChangePercentage { get; set; }
    public decimal ActualSavings { get; set; }
    public decimal SavingsRate { get; set; }
    public decimal? SavingsRateChangePercentage { get; set; }
    public decimal BudgetUsedPercentage { get; set; }
    public decimal? BudgetChangePercentage { get; set; }
    public List<string> Insights { get; set; } = [];
}
