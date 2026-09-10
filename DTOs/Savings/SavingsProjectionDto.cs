namespace PigPocket.Api.DTOs.Savings;

public class SavingsProjectionDto
{
    public decimal CurrentSavings { get; set; }
    public decimal MonthlySavingsAverage { get; set; }
    public List<ProjectionPointDto> Projections { get; set; } = [];
}
