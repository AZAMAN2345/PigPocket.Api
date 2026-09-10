using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.DTOs.Auth;
using PigPocket.Api.Services;

namespace PigPocket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;


    public AuthController(
        AuthService authService)
    {
        _authService = authService;
    }


    [HttpPost("register")]
    public async Task<IActionResult> Register(
        SignUpDTO dto)
    {
        var result =
            await _authService.Register(dto);


        if (result == null)
        {
            return Conflict(
                new
                {
                    message =
                        "An account with this email already exists."
                }
            );
        }


        return Ok(result);
    }


    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginDTO dto)
    {
        var result =
            await _authService.Login(dto);


        if (result == null)
        {
            return Unauthorized(
                new
                {
                    message =
                        "Invalid email or password."
                }
            );
        }


        return Ok(result);
    }
}
