namespace PigPocket.Api.DTOs.Analytics;

public class SavingsTrendPointDto
{
    public string Label { get; set; } = "";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Income { get; set; }
    public decimal ActualSavings { get; set; }
    public decimal ExpectedSavings { get; set; }
    public decimal ActualSavingsRate { get; set; }
    public decimal TargetSavingsRate { get; set; }
}

public class SavingsTrendDto
{
    public string Range { get; set; } = "";
    public List<SavingsTrendPointDto> Points { get; set; } = [];
}
