using MongoDB.Driver;
using PigPocket.Api.DTOs.Analytics;
using PigPocket.Api.Models;
using PigPocket.Api.Models.Enums;
using PigPocket.Api.Services.FinanceEngine;

namespace PigPocket.Api.Services.Analytics;

public class AnalyticsService
{
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly IMongoCollection<BudgetPlan> _budgets;
    private readonly IMongoCollection<User> _users;
    private readonly PeriodService _periodService;
    private readonly PeriodComparisonService _comparisonService;
    private readonly SpendingCalculator _spendingCalculator;
    private readonly SavingsCalculator _savingsCalculator;
    private readonly BudgetService _budgetService;

    public AnalyticsService(
        IMongoDatabase database,
        PeriodService periodService,
        PeriodComparisonService comparisonService,
        SpendingCalculator spendingCalculator,
        SavingsCalculator savingsCalculator,
        BudgetService budgetService)
    {
        _transactions = database.GetCollection<Transaction>("transactions");
        _budgets = database.GetCollection<BudgetPlan>("budgetPlans");
        _users = database.GetCollection<User>("Users");
        _periodService = periodService;
        _comparisonService = comparisonService;
        _spendingCalculator = spendingCalculator;
        _savingsCalculator = savingsCalculator;
        _budgetService = budgetService;
    }

    public async Task<AnalyticsSummaryDto> GetSummaryAsync(
        Guid userId,
        BudgetPeriod period,
        CancellationToken cancellationToken = default)
    {
        var range = _periodService.GetRange(period);
        var previousRange = _periodService.GetPreviousRange(range);
        var user = await _users.Find(existing => existing.Id == userId).FirstOrDefaultAsync(cancellationToken);
        var transactions = await _transactions.Find(transaction =>
                transaction.UserId == userId
                && transaction.TransactionDate >= previousRange.StartDate
                && transaction.TransactionDate <= range.EndDate)
            .ToListAsync(cancellationToken);
        var current = transactions.Where(transaction => TrendService.IsInRange(transaction, range)).ToList();
        var previous = transactions.Where(transaction => TrendService.IsInRange(transaction, previousRange)).ToList();

        var income = _spendingCalculator.CalculateEligibleIncome(current);
        var previousIncome = _spendingCalculator.CalculateEligibleIncome(previous);
        var spending = _spendingCalculator.CalculateEligibleSpending(current);
        var previousSpending = _spendingCalculator.CalculateEligibleSpending(previous);
        var actualSavings = _savingsCalculator.CalculateActualSavings(income, spending);
        var previousSavings = _savingsCalculator.CalculateActualSavings(previousIncome, previousSpending);
        var savingsRate = _savingsCalculator.CalculateSavingsRate(income, actualSavings);
        var previousSavingsRate = _savingsCalculator.CalculateSavingsRate(previousIncome, previousSavings);

        var currentBudget = await GetBudgetAsync(userId, period, range, cancellationToken);
        var previousBudget = await GetBudgetAsync(userId, period, previousRange, cancellationToken);
        var budgetUsed = await GetBudgetUsageAsync(userId, currentBudget, cancellationToken);
        var previousBudgetUsed = await GetBudgetUsageAsync(userId, previousBudget, cancellationToken);
        var spendingComparison = _comparisonService.Compare(spending, previousSpending);
        var insights = BuildInsights(
            spendingComparison,
            income,
            savingsRate,
            user?.TargetSavingsRate ?? 0,
            budgetUsed,
            range);

        return new AnalyticsSummaryDto
        {
            Income = income,
            IncomeChangePercentage = _comparisonService.Compare(income, previousIncome).PercentageChange,
            Spending = spending,
            SpendingChangePercentage = spendingComparison.PercentageChange,
            ActualSavings = actualSavings,
            SavingsRate = Math.Round(savingsRate, 2),
            SavingsRateChangePercentage = _comparisonService.Compare(savingsRate, previousSavingsRate).PercentageChange,
            BudgetUsedPercentage = budgetUsed,
            BudgetChangePercentage = previousBudget is null
                ? null
                : _comparisonService.Compare(budgetUsed, previousBudgetUsed).PercentageChange,
            Insights = insights
        };
    }

    private async Task<BudgetPlan?> GetBudgetAsync(
        Guid userId,
        BudgetPeriod period,
        DateRange range,
        CancellationToken cancellationToken)
    {
        return await _budgets.Find(plan =>
                plan.UserId == userId
                && plan.Period == period
                && plan.StartDate <= range.EndDate
                && plan.EndDate >= range.StartDate)
            .SortByDescending(plan => plan.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<decimal> GetBudgetUsageAsync(
        Guid userId,
        BudgetPlan? budget,
        CancellationToken cancellationToken)
    {
        if (budget is null)
        {
            return 0;
        }

        var result = await _budgetService.GetBudgetStatusAsync(userId, budget.Id, cancellationToken);
        return result.Value is null ? 0 : Math.Round(result.Value.PercentageUsed, 2);
    }

    private static List<string> BuildInsights(
        PeriodComparisonDto spendingComparison,
        decimal income,
        decimal savingsRate,
        decimal targetSavingsRate,
        decimal budgetUsed,
        DateRange range)
    {
        var insights = new List<string>();
        if (spendingComparison.PercentageChange is { } spendingChange)
        {
            if (spendingChange < 0)
            {
                insights.Add($"You spent {Math.Abs(spendingChange):0.##}% less than the previous period.");
            }
            else if (spendingChange > 0)
            {
                insights.Add($"You spent {spendingChange:0.##}% more than the previous period.");
            }
        }

        if (income > 0)
        {
            insights.Add(savingsRate >= targetSavingsRate
                ? "You beat your savings target this period."
                : "You are currently below your savings target.");
        }

        var totalTicks = Math.Max(1, range.EndDate.Ticks - range.StartDate.Ticks);
        var elapsedTicks = Math.Clamp(DateTime.UtcNow.Ticks - range.StartDate.Ticks, 0, totalTicks);
        var elapsedPercentage = (decimal)elapsedTicks / totalTicks * 100m;
        if (budgetUsed > elapsedPercentage + 5m)
        {
            insights.Add("Your budget is being used faster than expected.");
        }

        return insights;
    }
}
