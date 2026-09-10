using System.ComponentModel.DataAnnotations;
namespace PigPocket.Api.DTOs.Auth;

public class LoginDTO
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = "";
    [Required, MaxLength(128)] public string Password { get; set; } = "";
}
