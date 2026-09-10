using System.ComponentModel.DataAnnotations;

namespace PigPocket.Api.DTOs.Goals;

public class AddGoalContributionDto
{
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    public DateTime? ContributionDate { get; set; }
}
