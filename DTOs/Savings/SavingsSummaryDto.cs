namespace PigPocket.Api.DTOs.Savings;

public class SavingsSummaryDto
{
    public decimal Income { get; set; }
    public decimal Spending { get; set; }
    public decimal ActualSavings { get; set; }
    public decimal ActualSavingsRate { get; set; }
    public decimal TargetSavingsRate { get; set; }
    public decimal ExpectedSavings { get; set; }
    public decimal Difference { get; set; }
    public string Status { get; set; } = "";
}
