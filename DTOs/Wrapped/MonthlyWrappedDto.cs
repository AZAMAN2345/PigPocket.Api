namespace PigPocket.Api.DTOs.Wrapped;

public class MonthlyWrappedDto
{
    public string Period { get; set; } = "";
    public decimal Income { get; set; }
    public decimal Spending { get; set; }
    public decimal? SpendingChangePercentage { get; set; }
    public decimal ActualSavings { get; set; }
    public decimal ActualSavingsRate { get; set; }
    public decimal TargetSavingsRate { get; set; }
    public string SavingsStatus { get; set; } = "";
    public WrappedCategoryDto? TopCategory { get; set; }
    public WrappedMerchantDto? TopMerchant { get; set; }
    public int BudgetCategoriesWithinLimit { get; set; }
    public int TotalBudgetCategories { get; set; }
    public string? BestControlledCategory { get; set; }
    public string? WorstControlledCategory { get; set; }
    public decimal GoalContributionTotal { get; set; }
    public List<WrappedHighlightDto> Highlights { get; set; } = [];
}

public class WrappedCategoryDto
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal PercentageOfSpending { get; set; }
}

public class WrappedMerchantDto
{
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}
