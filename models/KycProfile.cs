namespace PigPocket.Api.Models;

public class KycProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool IsBvnVerified { get; set; }
    public bool IsNinVerified { get; set; }
    public string? BvnLastFour { get; set; }
    public string? NinLastFour { get; set; }
    public string? KycProviderReference { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
