using System.ComponentModel.DataAnnotations;

namespace PigPocket.Api.DTOs.Goals;

public class CreateSavingsGoalDto
{
    [Required]
    public string Name { get; set; } = "";

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal TargetAmount { get; set; }

    public DateTime? TargetDate { get; set; }
}
