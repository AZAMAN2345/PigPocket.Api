using MongoDB.Driver;
using PigPocket.Api.DTOs.Analytics;
using PigPocket.Api.Enums;
using PigPocket.Api.Models;
using PigPocket.Api.Models.Enums;

namespace PigPocket.Api.Services.Analytics;

public class CategoryAnalyticsService
{
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly IMongoCollection<Category> _categories;
    private readonly IMongoCollection<BudgetPlan> _budgets;
    private readonly PeriodService _periodService;
    private readonly PeriodComparisonService _comparisonService;

    public CategoryAnalyticsService(
        IMongoDatabase database,
        PeriodService periodService,
        PeriodComparisonService comparisonService)
    {
        _transactions = database.GetCollection<Transaction>("transactions");
        _categories = database.GetCollection<Category>("categories");
        _budgets = database.GetCollection<BudgetPlan>("budgetPlans");
        _periodService = periodService;
        _comparisonService = comparisonService;
    }

    public Task<CategoryAnalyticsDto> GetCategoryBreakdownAsync(
        Guid userId,
        BudgetPeriod period,
        TrendRange? trendRange,
        CancellationToken cancellationToken = default)
    {
        var range = trendRange.HasValue
            ? _periodService.GetTrendRange(trendRange.Value)
            : _periodService.GetRange(period);
        return GetCategoryBreakdownForRangeAsync(userId, range, cancellationToken);
    }

    public async Task<CategoryAnalyticsDto> GetCategoryBreakdownForRangeAsync(
        Guid userId,
        DateRange range,
        CancellationToken cancellationToken = default)
    {
        var previousRange = _periodService.GetPreviousRange(range);
        var currentGroups = await _transactions.Aggregate()
            .Match(transaction =>
                transaction.UserId == userId
                && transaction.CountsAsSpending
                && transaction.TransactionDate >= range.StartDate
                && transaction.TransactionDate <= range.EndDate)
            .Group(
                transaction => transaction.CategoryId,
                group => new CategoryAggregate
                {
                    CategoryId = group.Key,
                    Amount = group.Sum(transaction => transaction.Amount),
                    TransactionCount = group.Count()
                })
            .ToListAsync(cancellationToken);
        var previousGroups = await _transactions.Aggregate()
            .Match(transaction =>
                transaction.UserId == userId
                && transaction.CountsAsSpending
                && transaction.TransactionDate >= previousRange.StartDate
                && transaction.TransactionDate <= previousRange.EndDate)
            .Group(
                transaction => transaction.CategoryId,
                group => new CategoryAggregate
                {
                    CategoryId = group.Key,
                    Amount = group.Sum(transaction => transaction.Amount),
                    TransactionCount = group.Count()
                })
            .ToListAsync(cancellationToken);
        var categoryIds = currentGroups.Concat(previousGroups)
            .Where(group => group.CategoryId.HasValue)
            .Select(group => group.CategoryId!.Value)
            .Distinct()
            .ToArray();
        var categories = categoryIds.Length == 0
            ? []
            : await _categories.Find(category => categoryIds.Contains(category.Id)).ToListAsync(cancellationToken);
        var names = categories.ToDictionary(category => category.Id, category => category.Name);
        var total = currentGroups.Sum(group => group.Amount);

        var items = currentGroups
            .Select(group =>
            {
                var previousAmount = previousGroups.FirstOrDefault(previous =>
                    previous.CategoryId == group.CategoryId)?.Amount ?? 0;
                return new CategoryAnalyticsItemDto
                {
                    CategoryId = group.CategoryId,
                    Category = group.CategoryId.HasValue && names.TryGetValue(group.CategoryId.Value, out var name)
                        ? name
                        : "Uncategorized",
                    Amount = group.Amount,
                    PercentageOfSpending = total <= 0 ? 0 : Math.Round(group.Amount / total * 100m, 2),
                    TransactionCount = group.TransactionCount,
                    ChangeFromPreviousPeriod = _comparisonService.Compare(group.Amount, previousAmount).PercentageChange
                };
            })
            .OrderByDescending(item => item.Amount)
            .ToList();

        return new CategoryAnalyticsDto { TotalSpending = total, Categories = items };
    }

    public async Task<CategoryAnalyticsDetailDto?> GetCategoryDetailAsync(
        Guid userId,
        Guid categoryId,
        BudgetPeriod period,
        TrendRange? trendRange,
        CancellationToken cancellationToken = default)
    {
        var category = await _categories.Find(existing => existing.Id == categoryId)
            .FirstOrDefaultAsync(cancellationToken);
        if (category is null)
        {
            return null;
        }

        var range = trendRange.HasValue
            ? _periodService.GetTrendRange(trendRange.Value)
            : _periodService.GetRange(period);
        var previousRange = _periodService.GetPreviousRange(range);
        var transactions = await _transactions.Find(transaction =>
                transaction.UserId == userId
                && transaction.CountsAsSpending
                && transaction.CategoryId == categoryId
                && transaction.TransactionDate >= previousRange.StartDate
                && transaction.TransactionDate <= range.EndDate)
            .ToListAsync(cancellationToken);
        var current = transactions.Where(transaction => TrendService.IsInRange(transaction, range)).ToList();
        var previousAmount = transactions.Where(transaction => TrendService.IsInRange(transaction, previousRange))
            .Sum(transaction => transaction.Amount);
        var amount = current.Sum(transaction => transaction.Amount);
        var allSpending = await _transactions.Find(transaction =>
                transaction.UserId == userId
                && transaction.CountsAsSpending
                && transaction.TransactionDate >= range.StartDate
                && transaction.TransactionDate <= range.EndDate)
            .ToListAsync(cancellationToken);
        var totalSpending = allSpending.Sum(transaction => transaction.Amount);
        var budgets = await _budgets.Find(plan =>
                plan.UserId == userId
                && plan.StartDate <= range.EndDate
                && plan.EndDate >= range.StartDate
                && plan.Allocations.Any(allocation => allocation.CategoryId == categoryId))
            .ToListAsync(cancellationToken);
        var selectedBudgets = budgets
            .GroupBy(plan => new { plan.Period, plan.StartDate, plan.EndDate })
            .Select(group => group.OrderByDescending(plan => plan.UpdatedAt).First())
            .ToList();
        var budgetAmount = selectedBudgets.Count == 0
            ? (decimal?)null
            : selectedBudgets.Sum(plan =>
                plan.Allocations.First(allocation => allocation.CategoryId == categoryId).AllocatedAmount);

        return new CategoryAnalyticsDetailDto
        {
            CategoryId = categoryId,
            Category = category.Name,
            Amount = amount,
            PercentageOfSpending = totalSpending <= 0 ? 0 : Math.Round(amount / totalSpending * 100m, 2),
            TransactionCount = current.Count,
            ChangeFromPreviousPeriod = _comparisonService.Compare(amount, previousAmount).PercentageChange,
            AverageTransactionAmount = current.Count == 0 ? 0 : Math.Round(amount / current.Count, 2),
            LargestTransaction = current.Count == 0 ? 0 : current.Max(transaction => transaction.Amount),
            BudgetAmount = budgetAmount,
            BudgetPercentageUsed = budgetAmount is null or <= 0 ? null : Math.Round(amount / budgetAmount.Value * 100m, 2)
        };
    }

    private sealed class CategoryAggregate
    {
        public Guid? CategoryId { get; set; }
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }
}
