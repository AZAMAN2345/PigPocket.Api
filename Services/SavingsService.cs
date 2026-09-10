using MongoDB.Driver;
using PigPocket.Api.DTOs.Savings;
using PigPocket.Api.Models;
using PigPocket.Api.Models.Enums;
using PigPocket.Api.Services.FinanceEngine;

namespace PigPocket.Api.Services;

public class SavingsService
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly SavingsCalculator _savingsCalculator;
    private readonly SpendingCalculator _spendingCalculator;
    private readonly ProjectionCalculator _projectionCalculator;
    private readonly PeriodService _periodService;

    public SavingsService(
        IMongoDatabase database,
        SavingsCalculator savingsCalculator,
        SpendingCalculator spendingCalculator,
        ProjectionCalculator projectionCalculator,
        PeriodService periodService)
    {
        _users = database.GetCollection<User>("Users");
        _transactions = database.GetCollection<Transaction>("transactions");
        _savingsCalculator = savingsCalculator;
        _spendingCalculator = spendingCalculator;
        _projectionCalculator = projectionCalculator;
        _periodService = periodService;
    }

    public async Task<User?> UpdateSavingsTargetAsync(
        Guid userId,
        decimal targetSavingsRate,
        CancellationToken cancellationToken = default)
    {
        return await _users.FindOneAndUpdateAsync(
            user => user.Id == userId,
            Builders<User>.Update.Set(user => user.TargetSavingsRate, targetSavingsRate),
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<SavingsSummaryDto?> GetSavingsSummaryAsync(
        Guid userId,
        BudgetPeriod period,
        CancellationToken cancellationToken = default)
    {
        var user = await _users
            .Find(existing => existing.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var range = _periodService.GetRange(period);
        var transactions = await GetTransactionsInRangeAsync(userId, range, cancellationToken);
        var income = _spendingCalculator.CalculateEligibleIncome(transactions);
        var spending = _spendingCalculator.CalculateEligibleSpending(transactions);
        var actualSavings = _savingsCalculator.CalculateActualSavings(income, spending);
        var expectedSavings = _savingsCalculator.CalculateExpectedSavings(income, user.TargetSavingsRate);

        return new SavingsSummaryDto
        {
            Income = income,
            Spending = spending,
            ActualSavings = actualSavings,
            ActualSavingsRate = _savingsCalculator.CalculateSavingsRate(income, actualSavings),
            TargetSavingsRate = user.TargetSavingsRate,
            ExpectedSavings = expectedSavings,
            Difference = actualSavings - expectedSavings,
            Status = _savingsCalculator.GetSavingsStatus(actualSavings, expectedSavings)
        };
    }

    public async Task<SavingsProjectionDto?> GetProjectionAsync(
        Guid userId,
        int months,
        CancellationToken cancellationToken = default)
    {
        if (months <= 0 || months > 60)
        {
            return null;
        }

        var userExists = await _users
            .Find(user => user.Id == userId)
            .AnyAsync(cancellationToken);

        if (!userExists)
        {
            return null;
        }

        var monthlyRange = _periodService.GetMonthlyRange(DateTime.UtcNow);
        var monthlyTransactions = await GetTransactionsInRangeAsync(userId, monthlyRange, cancellationToken);
        var monthlyIncome = _spendingCalculator.CalculateEligibleIncome(monthlyTransactions);
        var monthlySpending = _spendingCalculator.CalculateEligibleSpending(monthlyTransactions);
        var monthlySavingsAverage = _savingsCalculator.CalculateActualSavings(monthlyIncome, monthlySpending);

        var allTransactions = await _transactions
            .Find(transaction =>
                transaction.UserId == userId
                && !transaction.IsInternalTransfer)
            .ToListAsync(cancellationToken);

        var currentSavings = _savingsCalculator.CalculateActualSavings(
            _spendingCalculator.CalculateEligibleIncome(allTransactions),
            _spendingCalculator.CalculateEligibleSpending(allTransactions));

        return new SavingsProjectionDto
        {
            CurrentSavings = currentSavings,
            MonthlySavingsAverage = monthlySavingsAverage,
            Projections = _projectionCalculator.ProjectSavings(currentSavings, monthlySavingsAverage, months)
        };
    }

    private async Task<List<Transaction>> GetTransactionsInRangeAsync(
        Guid userId,
        DateRange range,
        CancellationToken cancellationToken)
    {
        return await _transactions
            .Find(transaction =>
                transaction.UserId == userId
                && transaction.TransactionDate >= range.StartDate
                && transaction.TransactionDate <= range.EndDate)
            .ToListAsync(cancellationToken);
    }
}
