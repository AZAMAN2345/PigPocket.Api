using PigPocket.Api.DTOs.Analytics;

namespace PigPocket.Api.Services.Analytics;

public class PeriodComparisonService
{
    public PeriodComparisonDto Compare(decimal current, decimal previous)
    {
        decimal? percentage = previous == 0
            ? null
            : Math.Round((current - previous) / previous * 100m, 2);

        return new PeriodComparisonDto
        {
            CurrentValue = current,
            PreviousValue = previous,
            PercentageChange = percentage,
            Direction = previous == 0
                ? "NoPreviousData"
                : current > previous
                    ? "Increase"
                    : current < previous
                        ? "Decrease"
                        : "NoChange"
        };
    }
}
