namespace PigPocket.Api.DTOs.Goals;

public class SavingsGoalResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
    public decimal PercentageComplete { get; set; }
    public DateTime CreatedAt { get; set; }
}
