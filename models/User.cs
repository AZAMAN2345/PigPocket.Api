namespace PigPocket.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsEmailVerified { get; set; }
    public EmailChallenge? Verification { get; set; }
    public EmailChallenge? PasswordReset { get; set; }
    public List<AuthSession> Sessions { get; set; } = [];
    public decimal TargetSavingsRate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EmailChallenge
{
    public string Hash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime SentAt { get; set; }
    public int Attempts { get; set; }
}

public class AuthSession
{
    public string Id { get; set; } = "";
    public string RefreshHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
