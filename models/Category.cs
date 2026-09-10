namespace PigPocket.Api.Models;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public bool IsIncomeCategory { get; set; }
    public bool IsSystemCategory { get; set; }
}
