using MongoDB.Driver;
using PigPocket.Api.DTOs.Analytics;
using PigPocket.Api.Enums;
using PigPocket.Api.Models;
using PigPocket.Api.Models.Enums;

namespace PigPocket.Api.Services.Analytics;

public class MerchantAnalyticsService
{
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly PeriodService _periodService;

    public MerchantAnalyticsService(IMongoDatabase database, PeriodService periodService)
    {
        _transactions = database.GetCollection<Transaction>("transactions");
        _periodService = periodService;
    }

    public Task<MerchantAnalyticsDto> GetMerchantBreakdownAsync(
        Guid userId,
        BudgetPeriod period,
        TrendRange? trendRange,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var range = trendRange.HasValue
            ? _periodService.GetTrendRange(trendRange.Value)
            : _periodService.GetRange(period);
        return GetMerchantBreakdownForRangeAsync(userId, range, limit, cancellationToken);
    }

    public async Task<MerchantAnalyticsDto> GetMerchantBreakdownForRangeAsync(
        Guid userId,
        DateRange range,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var aggregates = await _transactions.Aggregate()
            .Match(transaction =>
                transaction.UserId == userId
                && transaction.CountsAsSpending
                && transaction.TransactionDate >= range.StartDate
                && transaction.TransactionDate <= range.EndDate)
            .Group(
                transaction => transaction.Merchant,
                group => new MerchantAggregate
                {
                    Merchant = group.Key,
                    Amount = group.Sum(transaction => transaction.Amount),
                    TransactionCount = group.Count()
                })
            .ToListAsync(cancellationToken);
        var total = aggregates.Sum(group => group.Amount);
        var merchants = aggregates
            .GroupBy(
                group => string.IsNullOrWhiteSpace(group.Merchant)
                    ? "Unknown Merchant"
                    : group.Merchant.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new MerchantAnalyticsItemDto
            {
                Merchant = group.Key,
                Amount = group.Sum(item => item.Amount),
                TransactionCount = group.Sum(item => item.TransactionCount)
            })
            .OrderByDescending(item => item.Amount)
            .ThenBy(item => item.Merchant)
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();

        foreach (var merchant in merchants)
        {
            merchant.PercentageOfSpending = total <= 0
                ? 0
                : Math.Round(merchant.Amount / total * 100m, 2);
        }

        return new MerchantAnalyticsDto { TotalSpending = total, Merchants = merchants };
    }

    private sealed class MerchantAggregate
    {
        public string Merchant { get; set; } = "";
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
    }
}
