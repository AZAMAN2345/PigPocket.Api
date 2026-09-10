namespace PigPocket.Api.DTOs.Analytics;

public class IncomeTrendPointDto
{
    public string Label { get; set; } = "";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Amount { get; set; }
}

public class IncomeTrendDto
{
    public string Range { get; set; } = "";
    public decimal Total { get; set; }
    public decimal? ChangePercentage { get; set; }
    public List<IncomeTrendPointDto> Points { get; set; } = [];
}
