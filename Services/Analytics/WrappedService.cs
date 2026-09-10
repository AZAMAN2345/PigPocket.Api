using System.Globalization;
using MongoDB.Driver;
using PigPocket.Api.DTOs.Wrapped;
using PigPocket.Api.Models;
using PigPocket.Api.Services.FinanceEngine;

namespace PigPocket.Api.Services.Analytics;

public class WrappedService
{
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly IMongoCollection<BudgetPlan> _budgets;
    private readonly IMongoCollection<Category> _categories;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<SavingsGoal> _goals;
    private readonly IMongoCollection<GoalContribution> _contributions;
    private readonly PeriodService _periodService;
    private readonly PeriodComparisonService _comparisonService;
    private readonly CategoryAnalyticsService _categoryAnalyticsService;
    private readonly MerchantAnalyticsService _merchantAnalyticsService;
    private readonly SpendingCalculator _spendingCalculator;
    private readonly SavingsCalculator _savingsCalculator;

    public WrappedService(
        IMongoDatabase database,
        PeriodService periodService,
        PeriodComparisonService comparisonService,
        CategoryAnalyticsService categoryAnalyticsService,
        MerchantAnalyticsService merchantAnalyticsService,
        SpendingCalculator spendingCalculator,
        SavingsCalculator savingsCalculator)
    {
        _transactions = database.GetCollection<Transaction>("transactions");
        _budgets = database.GetCollection<BudgetPlan>("budgetPlans");
        _categories = database.GetCollection<Category>("categories");
        _users = database.GetCollection<User>("Users");
        _goals = database.GetCollection<SavingsGoal>("savingsGoals");
        _contributions = database.GetCollection<GoalContribution>("goalContributions");
        _periodService = periodService;
        _comparisonService = comparisonService;
        _categoryAnalyticsService = categoryAnalyticsService;
        _merchantAnalyticsService = merchantAnalyticsService;
        _spendingCalculator = spendingCalculator;
        _savingsCalculator = savingsCalculator;
    }

    public async Task<MonthlyWrappedDto> GetMonthlyWrappedAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var range = _periodService.GetMonthlyRange(
            new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc));
        var previousRange = _periodService.GetPreviousRange(range);
        var transactions = await GetTransactionsAsync(userId, previousRange.StartDate, range.EndDate, cancellationToken);
        var current = transactions.Where(transaction => TrendService.IsInRange(transaction, range)).ToList();
        var previous = transactions.Where(transaction => TrendService.IsInRange(transaction, previousRange)).ToList();
        var income = _spendingCalculator.CalculateEligibleIncome(current);
        var spending = _spendingCalculator.CalculateEligibleSpending(current);
        var previousSpending = _spendingCalculator.CalculateEligibleSpending(previous);
        var actualSavings = _savingsCalculator.CalculateActualSavings(income, spending);
        var user = await _users.Find(existing => existing.Id == userId).FirstOrDefaultAsync(cancellationToken);
        var targetRate = user?.TargetSavingsRate ?? 0;
        var actualRate = _savingsCalculator.CalculateSavingsRate(income, actualSavings);
        var expectedSavings = _savingsCalculator.CalculateExpectedSavings(income, targetRate);
        var categoryBreakdown = await _categoryAnalyticsService.GetCategoryBreakdownForRangeAsync(
            userId, range, cancellationToken);
        var merchantBreakdown = await _merchantAnalyticsService.GetMerchantBreakdownForRangeAsync(
            userId, range, 1, cancellationToken);
        var budgetPerformance = await GetBudgetPerformanceAsync(userId, range, current, cancellationToken);
        var goalContributionTotal = await GetGoalContributionTotalAsync(userId, range, cancellationToken);
        var spendingChange = _comparisonService.Compare(spending, previousSpending).PercentageChange;
        var topCategory = categoryBreakdown.Categories.FirstOrDefault();
        var topMerchant = merchantBreakdown.Merchants.FirstOrDefault();
        var savingsStatus = _savingsCalculator.GetSavingsStatus(actualSavings, expectedSavings);

        var result = new MonthlyWrappedDto
        {
            Period = range.StartDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            Income = income,
            Spending = spending,
            SpendingChangePercentage = spendingChange,
            ActualSavings = actualSavings,
            ActualSavingsRate = Math.Round(actualRate, 2),
            TargetSavingsRate = targetRate,
            SavingsStatus = savingsStatus,
            TopCategory = topCategory is null ? null : new WrappedCategoryDto
            {
                Name = topCategory.Category,
                Amount = topCategory.Amount,
                PercentageOfSpending = topCategory.PercentageOfSpending
            },
            TopMerchant = topMerchant is null ? null : new WrappedMerchantDto
            {
                Name = topMerchant.Merchant,
                Amount = topMerchant.Amount,
                TransactionCount = topMerchant.TransactionCount
            },
            BudgetCategoriesWithinLimit = budgetPerformance.WithinLimit,
            TotalBudgetCategories = budgetPerformance.Total,
            BestControlledCategory = budgetPerformance.BestCategory,
            WorstControlledCategory = budgetPerformance.WorstCategory,
            GoalContributionTotal = goalContributionTotal
        };

        result.Highlights = BuildMonthlyHighlights(result);
        return result;
    }

    public async Task<YearlyWrappedDto> GetYearlyWrappedAsync(
        Guid userId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var range = new DateRange(
            new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1));
        var transactions = await GetTransactionsAsync(userId, range.StartDate, range.EndDate, cancellationToken);
        var user = await _users.Find(existing => existing.Id == userId).FirstOrDefaultAsync(cancellationToken);
        var targetRate = user?.TargetSavingsRate ?? 0;
        var months = Enumerable.Range(1, 12).Select(month =>
        {
            var monthRange = _periodService.GetMonthlyRange(
                new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc));
            var monthTransactions = transactions.Where(transaction => TrendService.IsInRange(transaction, monthRange)).ToList();
            var income = _spendingCalculator.CalculateEligibleIncome(monthTransactions);
            var spending = _spendingCalculator.CalculateEligibleSpending(monthTransactions);
            var savings = _savingsCalculator.CalculateActualSavings(income, spending);
            return new MonthMetric(
                month,
                monthTransactions.Count,
                income,
                spending,
                savings,
                _savingsCalculator.CalculateSavingsRate(income, savings));
        }).ToList();
        var spendingMonths = months.Where(month => month.Spending > 0).ToList();
        var incomeMonths = months.Where(month => month.Income > 0).ToList();
        var categoryBreakdown = await _categoryAnalyticsService.GetCategoryBreakdownForRangeAsync(
            userId, range, cancellationToken);
        var merchantBreakdown = await _merchantAnalyticsService.GetMerchantBreakdownForRangeAsync(
            userId, range, 1, cancellationToken);
        var budgets = await _budgets.Find(plan =>
                plan.UserId == userId
                && plan.StartDate <= range.EndDate
                && plan.EndDate >= range.StartDate)
            .ToListAsync(cancellationToken);
        var monthsWithinBudget = CountMonthsWithinBudget(months, budgets, transactions, year);
        var completedGoals = await _goals.CountDocumentsAsync(goal =>
            goal.UserId == userId && goal.IsCompleted && goal.CreatedAt <= range.EndDate,
            cancellationToken: cancellationToken);
        var contributionTotal = await GetGoalContributionTotalAsync(userId, range, cancellationToken);
        var largest = transactions.Where(transaction => transaction.CountsAsSpending)
            .OrderByDescending(transaction => transaction.Amount)
            .FirstOrDefault();
        var targetHitMonths = incomeMonths.Count(month => month.SavingsRate >= targetRate);

        var result = new YearlyWrappedDto
        {
            Year = year,
            TotalIncome = months.Sum(month => month.Income),
            TotalSpending = months.Sum(month => month.Spending),
            TotalSavings = months.Sum(month => month.Savings),
            AverageSavingsRate = incomeMonths.Count == 0 ? 0 : Math.Round(incomeMonths.Average(month => month.SavingsRate), 2),
            SavingsTargetHitMonths = targetHitMonths,
            SavingsTargetSuccessRate = Math.Round(targetHitMonths / 12m * 100m, 2),
            HighestSpendingMonth = MonthName(spendingMonths.OrderByDescending(month => month.Spending).FirstOrDefault()),
            LowestSpendingMonth = MonthName(spendingMonths.OrderBy(month => month.Spending).FirstOrDefault()),
            BestSavingsMonth = MonthName(incomeMonths.OrderByDescending(month => month.Savings).FirstOrDefault()),
            WorstSavingsMonth = MonthName(incomeMonths.OrderBy(month => month.Savings).FirstOrDefault()),
            TopCategory = categoryBreakdown.Categories.FirstOrDefault()?.Category,
            TopMerchant = merchantBreakdown.Merchants.FirstOrDefault()?.Merchant,
            TotalTransactions = transactions.Count,
            MonthsWithinBudget = monthsWithinBudget,
            CompletedGoals = checked((int)completedGoals),
            TotalGoalContributions = contributionTotal,
            LargestSpendingTransaction = largest is null ? null : new WrappedTransactionDto
            {
                Id = largest.Id,
                Merchant = string.IsNullOrWhiteSpace(largest.Merchant) ? "Unknown Merchant" : largest.Merchant,
                Amount = largest.Amount,
                TransactionDate = largest.TransactionDate
            }
        };

        result.Highlights = BuildYearlyHighlights(result);
        return result;
    }

    private async Task<BudgetPerformance> GetBudgetPerformanceAsync(
        Guid userId,
        DateRange range,
        IReadOnlyCollection<Transaction> transactions,
        CancellationToken cancellationToken)
    {
        var budget = await _budgets.Find(plan =>
                plan.UserId == userId
                && plan.Period == PigPocket.Api.Models.Enums.BudgetPeriod.Monthly
                && plan.StartDate <= range.EndDate
                && plan.EndDate >= range.StartDate)
            .SortByDescending(plan => plan.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (budget is null || budget.Allocations.Count == 0)
        {
            return new BudgetPerformance();
        }

        var categoryIds = budget.Allocations.Select(allocation => allocation.CategoryId).ToArray();
        var categories = await _categories.Find(category => categoryIds.Contains(category.Id))
            .ToListAsync(cancellationToken);
        var names = categories.ToDictionary(category => category.Id, category => category.Name);
        var statuses = budget.Allocations.Select(allocation =>
        {
            var spent = transactions.Where(transaction =>
                    transaction.CountsAsSpending && transaction.CategoryId == allocation.CategoryId)
                .Sum(transaction => transaction.Amount);
            var percentage = allocation.AllocatedAmount <= 0 ? 0 : spent / allocation.AllocatedAmount * 100m;
            return new CategoryBudgetMetric(
                names.GetValueOrDefault(allocation.CategoryId, "Unknown Category"),
                spent,
                allocation.AllocatedAmount,
                percentage);
        }).ToList();

        return new BudgetPerformance(
            statuses.Count(status => status.Spent <= status.Budget),
            statuses.Count,
            statuses.OrderBy(status => status.PercentageUsed).First().Name,
            statuses.OrderByDescending(status => status.PercentageUsed).First().Name);
    }

    private static int CountMonthsWithinBudget(
        IReadOnlyCollection<MonthMetric> months,
        IReadOnlyCollection<BudgetPlan> budgets,
        IReadOnlyCollection<Transaction> transactions,
        int year)
    {
        var count = 0;
        foreach (var month in months)
        {
            var start = new DateTime(year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1).AddTicks(-1);
            var matchingBudgets = budgets.Where(plan => plan.StartDate <= end && plan.EndDate >= start).ToList();
            var budget = matchingBudgets
                .Where(plan => plan.Period == PigPocket.Api.Models.Enums.BudgetPeriod.Monthly)
                .OrderByDescending(plan => plan.UpdatedAt)
                .FirstOrDefault();
            budget ??= matchingBudgets.OrderByDescending(plan => plan.UpdatedAt).FirstOrDefault();
            if (budget is null)
            {
                continue;
            }

            var categoryIds = budget.Allocations.Select(allocation => allocation.CategoryId).ToHashSet();
            var spent = transactions.Where(transaction =>
                    transaction.CountsAsSpending
                    && transaction.TransactionDate >= start
                    && transaction.TransactionDate <= end
                    && transaction.CategoryId.HasValue
                    && categoryIds.Contains(transaction.CategoryId.Value))
                .Sum(transaction => transaction.Amount);
            if (spent <= budget.TotalBudgetAmount)
            {
                count++;
            }
        }

        return count;
    }

    private async Task<decimal> GetGoalContributionTotalAsync(
        Guid userId,
        DateRange range,
        CancellationToken cancellationToken)
    {
        var contributions = await _contributions.Find(contribution =>
                contribution.UserId == userId
                && contribution.ContributionDate >= range.StartDate
                && contribution.ContributionDate <= range.EndDate)
            .ToListAsync(cancellationToken);
        return contributions.Sum(contribution => contribution.Amount);
    }

    private async Task<List<Transaction>> GetTransactionsAsync(
        Guid userId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        return await _transactions.Find(transaction =>
                transaction.UserId == userId
                && transaction.TransactionDate >= start
                && transaction.TransactionDate <= end)
            .ToListAsync(cancellationToken);
    }

    private static List<WrappedHighlightDto> BuildMonthlyHighlights(MonthlyWrappedDto wrapped)
    {
        var highlights = new List<WrappedHighlightDto>();
        if (wrapped.SpendingChangePercentage is { } change)
        {
            highlights.Add(new WrappedHighlightDto
            {
                Type = "SpendingComparison",
                Message = change < 0
                    ? $"You spent {Math.Abs(change):0.##}% less than the previous month."
                    : change > 0
                        ? $"You spent {change:0.##}% more than the previous month."
                        : "Your spending was unchanged from the previous month."
            });
        }

        if (wrapped.Income > 0)
        {
            highlights.Add(new WrappedHighlightDto
            {
                Type = "SavingsTarget",
                Message = wrapped.SavingsStatus == "Behind"
                    ? "You were below your savings target this month."
                    : "You met or beat your savings target this month."
            });
        }
        if (wrapped.TopCategory is not null)
        {
            highlights.Add(new WrappedHighlightDto
            {
                Type = "TopCategory",
                Message = $"{wrapped.TopCategory.Name} was your biggest spending category."
            });
        }

        return highlights;
    }

    private static List<WrappedHighlightDto> BuildYearlyHighlights(YearlyWrappedDto wrapped)
    {
        var highlights = new List<WrappedHighlightDto>();
        if (wrapped.TotalIncome > 0)
        {
            highlights.Add(new WrappedHighlightDto
            {
                Type = "SavingsTarget",
                Message = $"You hit your savings target in {wrapped.SavingsTargetHitMonths} of 12 months."
            });
        }
        if (wrapped.TopCategory is not null)
        {
            highlights.Add(new WrappedHighlightDto
            {
                Type = "TopCategory",
                Message = $"{wrapped.TopCategory} was your top spending category for the year."
            });
        }

        return highlights;
    }

    private static string? MonthName(MonthMetric? metric)
        => metric is null ? null : CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(metric.Month);

    private sealed record MonthMetric(
        int Month,
        int TransactionCount,
        decimal Income,
        decimal Spending,
        decimal Savings,
        decimal SavingsRate);

    private sealed record CategoryBudgetMetric(
        string Name,
        decimal Spent,
        decimal Budget,
        decimal PercentageUsed);

    private sealed record BudgetPerformance(
        int WithinLimit = 0,
        int Total = 0,
        string? BestCategory = null,
        string? WorstCategory = null);
}
