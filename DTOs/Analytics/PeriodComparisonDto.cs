namespace PigPocket.Api.DTOs.Analytics;

public class PeriodComparisonDto
{
    public decimal CurrentValue { get; set; }
    public decimal PreviousValue { get; set; }
    public decimal? PercentageChange { get; set; }
    public string Direction { get; set; } = "";
}
