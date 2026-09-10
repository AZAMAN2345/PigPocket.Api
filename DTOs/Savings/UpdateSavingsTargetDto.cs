using System.ComponentModel.DataAnnotations;

namespace PigPocket.Api.DTOs.Savings;

public class UpdateSavingsTargetDto
{
    [Range(0, 100)]
    public decimal TargetSavingsRate { get; set; }
}
