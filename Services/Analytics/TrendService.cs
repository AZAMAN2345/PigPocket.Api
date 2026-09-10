using System.Globalization;
using MongoDB.Driver;
using PigPocket.Api.DTOs.Analytics;
using PigPocket.Api.Enums;
using PigPocket.Api.Models;
using PigPocket.Api.Models.Enums;
using PigPocket.Api.Services.FinanceEngine;

namespace PigPocket.Api.Services.Analytics;

public class TrendService
{
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly IMongoCollection<BudgetPlan> _budgets;
    private readonly IMongoCollection<User> _users;
    private readonly PeriodService _periodService;
    private readonly PeriodComparisonService _comparisonService;
    private readonly SpendingCalculator _spendingCalculator;
    private readonly SavingsCalculator _savingsCalculator;

    public TrendService(
        IMongoDatabase database,
        PeriodService periodService,
        PeriodComparisonService comparisonService,
        SpendingCalculator spendingCalculator,
        SavingsCalculator savingsCalculator)
    {
        _transactions = database.GetCollection<Transaction>("transactions");
        _budgets = database.GetCollection<BudgetPlan>("budgetPlans");
        _users = database.GetCollection<User>("Users");
        _periodService = periodService;
        _comparisonService = comparisonService;
        _spendingCalculator = spendingCalculator;
        _savingsCalculator = savingsCalculator;
    }

    public async Task<SpendingTrendDto> GetSpendingTrendAsync(
        Guid userId,
        TrendRange range,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var currentRange = _periodService.GetTrendRange(range);
        var previousRange = _periodService.GetPreviousRange(currentRange);
        var transactions = await GetTransactionsAsync(userId, previousRange.StartDate, currentRange.EndDate, cancellationToken);
        var current = transactions.Where(transaction => IsInRange(transaction, currentRange)
            && transaction.CountsAsSpending
            && (!categoryId.HasValue || transaction.CategoryId == categoryId));
        var previousTotal = transactions.Where(transaction => IsInRange(transaction, previousRange)
                && transaction.CountsAsSpending
                && (!categoryId.HasValue || transaction.CategoryId == categoryId))
            .Sum(transaction => transaction.Amount);
        var currentList = current.ToList();
        var total = currentList.Sum(transaction => transaction.Amount);

        return new SpendingTrendDto
        {
            Range = range.ToString(),
            Total = total,
            ChangePercentage = _comparisonService.Compare(total, previousTotal).PercentageChange,
            Points = transactions.Count == 0 ? [] : BuildBuckets(range, currentRange)
                .Select(bucket => new SpendingTrendPointDto
                {
                    Label = GetLabel(bucket, _periodService.GetGranularity(range)),
                    PeriodStart = bucket.StartDate,
                    PeriodEnd = bucket.EndDate,
                    Amount = currentList.Where(transaction => IsInRange(transaction, bucket))
                        .Sum(transaction => transaction.Amount)
                })
                .ToList()
        };
    }

    public async Task<IncomeTrendDto> GetIncomeTrendAsync(
        Guid userId,
        TrendRange range,
        CancellationToken cancellationToken = default)
    {
        var currentRange = _periodService.GetTrendRange(range);
        var previousRange = _periodService.GetPreviousRange(currentRange);
        var transactions = await GetTransactionsAsync(userId, previousRange.StartDate, currentRange.EndDate, cancellationToken);
        var current = transactions.Where(transaction => IsInRange(transaction, currentRange)).ToList();
        var previous = transactions.Where(transaction => IsInRange(transaction, previousRange)).ToList();
        var total = _spendingCalculator.CalculateEligibleIncome(current);
        var previousTotal = _spendingCalculator.CalculateEligibleIncome(previous);

        return new IncomeTrendDto
        {
            Range = range.ToString(),
            Total = total,
            ChangePercentage = _comparisonService.Compare(total, previousTotal).PercentageChange,
            Points = transactions.Count == 0 ? [] : BuildBuckets(range, currentRange)
                .Select(bucket => new IncomeTrendPointDto
                {
                    Label = GetLabel(bucket, _periodService.GetGranularity(range)),
                    PeriodStart = bucket.StartDate,
                    PeriodEnd = bucket.EndDate,
                    Amount = _spendingCalculator.CalculateEligibleIncome(
                        current.Where(transaction => IsInRange(transaction, bucket)))
                })
                .ToList()
        };
    }

    public async Task<BudgetTrendDto> GetBudgetTrendAsync(
        Guid userId,
        TrendRange range,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var currentRange = _periodService.GetTrendRange(range);
        var transactions = await GetTransactionsAsync(userId, currentRange.StartDate, currentRange.EndDate, cancellationToken);
        var budgets = await _budgets.Find(plan =>
                plan.UserId == userId
                && plan.StartDate <= currentRange.EndDate
                && plan.EndDate >= currentRange.StartDate)
            .ToListAsync(cancellationToken);

        var points = budgets.Count == 0 && transactions.Count == 0
            ? []
            : BuildBuckets(range, currentRange).Select(bucket =>
        {
            var budget = budgets
                .Where(plan => plan.StartDate <= bucket.EndDate && plan.EndDate >= bucket.StartDate)
                .OrderByDescending(plan => plan.UpdatedAt)
                .FirstOrDefault();
            var budgetAmount = budget is null
                ? 0
                : categoryId.HasValue
                    ? budget.Allocations.FirstOrDefault(allocation => allocation.CategoryId == categoryId)?.AllocatedAmount ?? 0
                    : budget.TotalBudgetAmount;
            var spent = transactions.Where(transaction =>
                    transaction.CountsAsSpending
                    && IsInRange(transaction, bucket)
                    && (!categoryId.HasValue || transaction.CategoryId == categoryId))
                .Sum(transaction => transaction.Amount);

            return new BudgetTrendPointDto
            {
                Label = GetLabel(bucket, _periodService.GetGranularity(range)),
                PeriodStart = bucket.StartDate,
                PeriodEnd = bucket.EndDate,
                BudgetAmount = budgetAmount,
                Spent = spent,
                Remaining = Math.Max(0, budgetAmount - spent),
                PercentageUsed = budgetAmount <= 0 ? 0 : Math.Round(spent / budgetAmount * 100m, 2),
                WasOverBudget = budgetAmount > 0 && spent > budgetAmount
            };
        }).ToList();

        return new BudgetTrendDto { Range = range.ToString(), Points = points };
    }

    public async Task<SavingsTrendDto> GetSavingsTrendAsync(
        Guid userId,
        TrendRange range,
        CancellationToken cancellationToken = default)
    {
        var currentRange = _periodService.GetTrendRange(range);
        var user = await _users.Find(existing => existing.Id == userId).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new SavingsTrendDto { Range = range.ToString() };
        }

        var transactions = await GetTransactionsAsync(userId, currentRange.StartDate, currentRange.EndDate, cancellationToken);
        if (transactions.Count == 0)
        {
            return new SavingsTrendDto { Range = range.ToString() };
        }

        var points = BuildBuckets(range, currentRange).Select(bucket =>
        {
            var bucketTransactions = transactions.Where(transaction => IsInRange(transaction, bucket)).ToList();
            var income = _spendingCalculator.CalculateEligibleIncome(bucketTransactions);
            var spending = _spendingCalculator.CalculateEligibleSpending(bucketTransactions);
            var actualSavings = _savingsCalculator.CalculateActualSavings(income, spending);

            return new SavingsTrendPointDto
            {
                Label = GetLabel(bucket, _periodService.GetGranularity(range)),
                PeriodStart = bucket.StartDate,
                PeriodEnd = bucket.EndDate,
                Income = income,
                ActualSavings = actualSavings,
                ExpectedSavings = _savingsCalculator.CalculateExpectedSavings(income, user.TargetSavingsRate),
                ActualSavingsRate = Math.Round(_savingsCalculator.CalculateSavingsRate(income, actualSavings), 2),
                TargetSavingsRate = user.TargetSavingsRate
            };
        }).ToList();

        return new SavingsTrendDto { Range = range.ToString(), Points = points };
    }

    private IReadOnlyList<DateRange> BuildBuckets(TrendRange range, DateRange dateRange)
        => _periodService.GetBuckets(dateRange, _periodService.GetGranularity(range));

    internal static string GetLabel(DateRange range, TrendGranularity granularity)
    {
        return granularity switch
        {
            TrendGranularity.Daily => range.StartDate.ToString("dd MMM", CultureInfo.InvariantCulture),
            TrendGranularity.Weekly => range.StartDate.ToString("dd MMM", CultureInfo.InvariantCulture),
            TrendGranularity.Monthly => range.StartDate.ToString("MMM", CultureInfo.InvariantCulture),
            _ => range.StartDate.ToString("d", CultureInfo.InvariantCulture)
        };
    }

    internal static bool IsInRange(Transaction transaction, DateRange range)
        => transaction.TransactionDate >= range.StartDate && transaction.TransactionDate <= range.EndDate;

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
}
