namespace PigPocket.Api.DTOs.Wrapped;

public class YearlyWrappedDto
{
    public int Year { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalSpending { get; set; }
    public decimal TotalSavings { get; set; }
    public decimal AverageSavingsRate { get; set; }
    public int SavingsTargetHitMonths { get; set; }
    public decimal SavingsTargetSuccessRate { get; set; }
    public string? HighestSpendingMonth { get; set; }
    public string? LowestSpendingMonth { get; set; }
    public string? BestSavingsMonth { get; set; }
    public string? WorstSavingsMonth { get; set; }
    public string? TopCategory { get; set; }
    public string? TopMerchant { get; set; }
    public int TotalTransactions { get; set; }
    public int MonthsWithinBudget { get; set; }
    public int CompletedGoals { get; set; }
    public decimal TotalGoalContributions { get; set; }
    public WrappedTransactionDto? LargestSpendingTransaction { get; set; }
    public List<WrappedHighlightDto> Highlights { get; set; } = [];
}

public class WrappedTransactionDto
{
    public Guid Id { get; set; }
    public string Merchant { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
}
