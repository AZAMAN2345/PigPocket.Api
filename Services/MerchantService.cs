using System.Text.RegularExpressions;
using PigPocket.Api.Services.Banking;

namespace PigPocket.Api.Services;

public class MerchantService
{
    public MerchantResolution ResolveMerchant(MonoTransactionItem transaction)
    {
        var payee = FirstMeaningful(
            transaction.Metadata?.Payee,
            transaction.Metadata?.Merchant,
            transaction.Metadata?.Name);

        if (payee is not null)
        {
            return new MerchantResolution(payee, MerchantResolutionSource.ProviderMetadata);
        }

        var narrationMerchant = CleanNarration(transaction.Narration);

        if (!string.IsNullOrWhiteSpace(narrationMerchant))
        {
            return new MerchantResolution(narrationMerchant, MerchantResolutionSource.Narration);
        }

        return new MerchantResolution("Unknown Merchant", MerchantResolutionSource.Unknown);
    }

    public MerchantResolution ResolveManualMerchant(
        string? merchant,
        string? description)
    {
        var meaningfulMerchant = FirstMeaningful(merchant);

        if (meaningfulMerchant is not null)
        {
            return new MerchantResolution(meaningfulMerchant, MerchantResolutionSource.UserInput);
        }

        var cleanedDescription = CleanNarration(description);

        if (!string.IsNullOrWhiteSpace(cleanedDescription))
        {
            return new MerchantResolution(cleanedDescription, MerchantResolutionSource.Narration);
        }

        return new MerchantResolution("Unknown Merchant", MerchantResolutionSource.Unknown);
    }

    private static string? FirstMeaningful(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var cleaned = value.Trim();

            if (cleaned.Length < 2
                || cleaned.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                || cleaned.Equals("n/a", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return cleaned;
        }

        return null;
    }

    private static string? CleanNarration(string? narration)
    {
        if (string.IsNullOrWhiteSpace(narration))
        {
            return null;
        }

        var cleaned = narration.Trim();
        cleaned = Regex.Replace(cleaned, @"\b(pos|web|ussd|nip|trf|transfer|payment|purchase)\b[:\-/\s]*", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b\d{8,}\b", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '-', ':', '/', '|');

        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}

public record MerchantResolution(
    string Merchant,
    MerchantResolutionSource Source);

public enum MerchantResolutionSource
{
    ProviderMetadata,
    UserInput,
    Narration,
    Unknown
}
