using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PigPocket.Api.DTOs.Kyc;
using PigPocket.Api.Services;

namespace PigPocket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class KycController : ControllerBase
{
    private readonly KycService _kycService;

    public KycController(KycService kycService)
    {
        _kycService = kycService;
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(
        VerifyKycDto dto,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid or missing user identity."
            });
        }

        var result = await _kycService.VerifyAsync(
            userId,
            dto.Bvn,
            dto.Nin,
            dto.DateOfBirth,
            cancellationToken);

        if (!result.IsVerified)
        {
            return BadRequest(new
            {
                message = "BVN/NIN verification failed."
            });
        }

        return Ok(new
        {
            message = "Identity verified successfully.",
            bvnVerified = true,
            ninVerified = true
        });
    }
}
