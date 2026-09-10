using System.ComponentModel.DataAnnotations;
namespace PigPocket.Api.DTOs.Auth;

public class SignUpDTO
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; set; } = "";
}
