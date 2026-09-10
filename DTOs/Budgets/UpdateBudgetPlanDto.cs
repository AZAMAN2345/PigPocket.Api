using System.ComponentModel.DataAnnotations;
using PigPocket.Api.Models.Enums;

namespace PigPocket.Api.DTOs.Budgets;

public class UpdateBudgetPlanDto
{
    [Required]
    public string Name { get; set; } = "";

    public BudgetPeriod Period { get; set; }
    public BudgetInputMode InputMode { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal TotalBudgetAmount { get; set; }

    public DateTime StartDate { get; set; }
    public List<BudgetAllocationDto> Allocations { get; set; } = [];
}
