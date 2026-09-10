using PigPocket.Api.Models.Enums;
using PigPocket.Api.Enums;

namespace PigPocket.Api.Services;

public class PeriodService
{
    public DateRange GetRange(BudgetPeriod period, DateTime? now = null)
    {
        var current = EnsureUtc(now ?? DateTime.UtcNow);

        return period switch
        {
            BudgetPeriod.Daily => GetDailyRange(current),
            BudgetPeriod.Weekly => GetWeeklyRange(current),
            BudgetPeriod.Monthly => GetMonthlyRange(current),
            _ => GetMonthlyRange(current)
        };
    }

    public DateRange GetDailyRange(DateTime date)
    {
        date = EnsureUtc(date);
        var start = date.Date;

        return new DateRange(start, start.AddDays(1).AddTicks(-1));
    }

    public DateRange GetWeeklyRange(DateTime date)
    {
        date = EnsureUtc(date);
        var start = date.Date.AddDays(-DaysSinceMonday(date));

        return new DateRange(start, start.AddDays(7).AddTicks(-1));
    }

    public DateRange GetMonthlyRange(DateTime date)
    {
        date = EnsureUtc(date);
        var start = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return new DateRange(start, start.AddMonths(1).AddTicks(-1));
    }

    public DateRange GetTrendRange(TrendRange range, DateTime? now = null)
    {
        var current = EnsureUtc(now ?? DateTime.UtcNow);
        var end = current.Date.AddDays(1).AddTicks(-1);

        return range switch
        {
            TrendRange.SevenDays => new DateRange(current.Date.AddDays(-6), end),
            TrendRange.OneMonth => new DateRange(current.Date.AddMonths(-1), end),
            TrendRange.ThreeMonths => new DateRange(current.Date.AddMonths(-3), end),
            TrendRange.SixMonths => new DateRange(
                new DateTime(current.Year, current.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5),
                end),
            TrendRange.OneYear => new DateRange(
                new DateTime(current.Year, current.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11),
                end),
            _ => new DateRange(current.Date.AddMonths(-1), end)
        };
    }

    public DateRange GetPreviousRange(DateRange range)
    {
        var fullMonthEnd = range.StartDate.AddMonths(1).AddTicks(-1);
        if (range.StartDate.Day == 1
            && range.StartDate.TimeOfDay == TimeSpan.Zero
            && range.EndDate == fullMonthEnd)
        {
            return new DateRange(range.StartDate.AddMonths(-1), range.StartDate.AddTicks(-1));
        }

        var end = range.StartDate.AddTicks(-1);
        var start = end - (range.EndDate - range.StartDate);
        return new DateRange(start, end);
    }

    public TrendGranularity GetGranularity(TrendRange range)
    {
        return range switch
        {
            TrendRange.SevenDays => TrendGranularity.Daily,
            TrendRange.OneMonth => TrendGranularity.Daily,
            TrendRange.ThreeMonths => TrendGranularity.Weekly,
            _ => TrendGranularity.Monthly
        };
    }

    public IReadOnlyList<DateRange> GetBuckets(DateRange range, TrendGranularity granularity)
    {
        var buckets = new List<DateRange>();
        var cursor = range.StartDate;

        while (cursor <= range.EndDate)
        {
            var naturalEnd = granularity switch
            {
                TrendGranularity.Daily => cursor.Date.AddDays(1).AddTicks(-1),
                TrendGranularity.Weekly => GetWeeklyRange(cursor).EndDate,
                TrendGranularity.Monthly => new DateTime(cursor.Year, cursor.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddMonths(1).AddTicks(-1),
                _ => cursor.Date.AddDays(1).AddTicks(-1)
            };

            var bucketEnd = naturalEnd < range.EndDate ? naturalEnd : range.EndDate;
            buckets.Add(new DateRange(cursor, bucketEnd));
            cursor = bucketEnd.AddTicks(1);
        }

        return buckets;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static int DaysSinceMonday(DateTime date)
    {
        return ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
    }
}

public record DateRange(
    DateTime StartDate,
    DateTime EndDate);
