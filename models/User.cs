namespace PigPocket.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public decimal TargetSavingsRate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
