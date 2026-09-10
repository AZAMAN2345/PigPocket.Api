using System.ComponentModel.DataAnnotations;
namespace PigPocket.Api.DTOs.Auth;

public class EmailDTO
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
}
public class VerifyEmailDTO : EmailDTO
{
    [Required, RegularExpression("^[0-9]{6}$")] public string Code { get; set; } = "";
}
public class ResetPasswordDTO : VerifyEmailDTO
{
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; set; } = "";
}
public class RefreshDTO
{
    [Required, MaxLength(200)] public string RefreshToken { get; set; } = "";
}
