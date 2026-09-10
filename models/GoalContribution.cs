namespace PigPocket.Api.Models;

public class GoalContribution
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SavingsGoalId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ContributionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
