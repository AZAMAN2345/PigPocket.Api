using System.ComponentModel.DataAnnotations;

namespace PigPocket.Api.DTOs.Kyc;

public class VerifyKycDto
{
    [Required]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "BVN must be an 11-digit string.")]
    public string Bvn { get; set; } = "";

    [Required]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "NIN must be an 11-digit string.")]
    public string Nin { get; set; } = "";

    [Required]
    public DateTime DateOfBirth { get; set; }
}
