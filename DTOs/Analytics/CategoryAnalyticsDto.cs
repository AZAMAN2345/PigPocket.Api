namespace PigPocket.Api.DTOs.Analytics;

public class CategoryAnalyticsItemDto
{
    public Guid? CategoryId { get; set; }
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal PercentageOfSpending { get; set; }
    public int TransactionCount { get; set; }
    public decimal? ChangeFromPreviousPeriod { get; set; }
}

public class CategoryAnalyticsDto
{
    public decimal TotalSpending { get; set; }
    public List<CategoryAnalyticsItemDto> Categories { get; set; } = [];
}

public class CategoryAnalyticsDetailDto : CategoryAnalyticsItemDto
{
    public decimal AverageTransactionAmount { get; set; }
    public decimal LargestTransaction { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? BudgetPercentageUsed { get; set; }
}
