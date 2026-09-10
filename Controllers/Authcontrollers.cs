using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PigPocket.Api.DTOs.Auth;
using PigPocket.Api.Services;
using System.Security.Claims;

namespace PigPocket.Api.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")] public async Task<IActionResult> Register(SignUpDTO dto) => Ok(await auth.Register(dto));
    [HttpPost("login")] public async Task<IActionResult> Login(LoginDTO dto) => Ok(await auth.Login(dto));
    [HttpPost("verify-email")] public async Task<IActionResult> Verify(VerifyEmailDTO dto) => Ok(await auth.VerifyEmail(dto));
    [HttpPost("resend-verification")] public async Task<IActionResult> Resend(EmailDTO dto)
    {
        await auth.SendCode(dto.Email, false);
        return Ok(new { message = "If your account needs verification, a code has been sent.", resendAfter = 45 });
    }
    [HttpPost("forgot-password")] public async Task<IActionResult> Forgot(EmailDTO dto)
    {
        await auth.SendCode(dto.Email, true);
        return Ok(new { message = "If an account exists, a password reset code has been sent.", resendAfter = 45 });
    }
    [HttpPost("reset-password")] public async Task<IActionResult> Reset(ResetPasswordDTO dto)
    {
        await auth.ResetPassword(dto);
        return Ok(new { message = "Password changed. Please log in again." });
    }
    [HttpPost("refresh")] public async Task<IActionResult> Refresh(RefreshDTO dto) => Ok(await auth.Refresh(dto.RefreshToken));
    [Authorize, HttpPost("logout")] public async Task<IActionResult> Logout()
    {
        await auth.Logout(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), User.FindFirstValue("sid")!);
        return NoContent();
    }
    [Authorize, HttpGet("me")] public IActionResult Me() => Ok(new { message = "Welcome to Piggy Pockets", email = User.FindFirstValue(ClaimTypes.Email) });
}
