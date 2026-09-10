namespace PigPocket.Api.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int ExpiresIn { get; set; }

    public Guid UserId { get; set; }

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public string Email { get; set; } = "";
}
