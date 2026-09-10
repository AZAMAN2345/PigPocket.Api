using MongoDB.Driver;
using PigPocket.Api.Models;
using PigPocket.Api.Services.Banking;

namespace PigPocket.Api.Services;

public class KycService
{
    private readonly IMongoCollection<KycProfile> _kycProfiles;
    private readonly MonoKycService _monoKycService;

    public KycService(
        IMongoDatabase database,
        MonoKycService monoKycService)
    {
        _kycProfiles = database.GetCollection<KycProfile>("kycProfiles");
        _monoKycService = monoKycService;
    }

    public async Task<KycVerificationResult> VerifyAsync(
        Guid userId,
        string bvn,
        string nin,
        DateTime dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        var verification = await _monoKycService.VerifyAsync(
            bvn,
            nin,
            dateOfBirth,
            cancellationToken);

        if (!verification.IsVerified)
        {
            return KycVerificationResult.Failed();
        }

        var existingProfile = await _kycProfiles
            .Find(profile => profile.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var profile = existingProfile ?? new KycProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now
        };

        profile.IsBvnVerified = true;
        profile.IsNinVerified = true;
        profile.BvnLastFour = bvn[^4..];
        profile.NinLastFour = nin[^4..];
        profile.KycProviderReference = verification.ProviderReference;
        profile.VerifiedAt = now;

        await _kycProfiles.ReplaceOneAsync(
            existing => existing.UserId == userId,
            profile,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        return KycVerificationResult.Succeeded();
    }
}

public class KycVerificationResult
{
    private KycVerificationResult(bool isVerified)
    {
        IsVerified = isVerified;
    }

    public bool IsVerified { get; }

    public static KycVerificationResult Succeeded()
    {
        return new KycVerificationResult(true);
    }

    public static KycVerificationResult Failed()
    {
        return new KycVerificationResult(false);
    }
}
