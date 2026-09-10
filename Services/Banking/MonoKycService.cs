using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PigPocket.Api.Services.Banking;

public class MonoKycService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MonoKycService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<MonoKycVerificationResult> VerifyAsync(
        string bvn,
        string nin,
        DateTime dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        var secretKey = _configuration["Mono:SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return MonoKycVerificationResult.Failed("Mono secret key is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v3/lookup/mashup");

        request.Headers.Add("mono-sec-key", secretKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        request.Content = JsonContent.Create(new MonoMashupLookupRequest
        {
            Nin = nin,
            Bvn = bvn,
            DateOfBirth = dateOfBirth.ToString("yyyy-MM-dd")
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return MonoKycVerificationResult.Failed("BVN/NIN verification failed.");
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return MonoKycVerificationResult.Succeeded(null);
        }

        using var document = JsonDocument.Parse(responseBody);

        if (ProviderResponseIndicatesFailure(document.RootElement))
        {
            return MonoKycVerificationResult.Failed("BVN/NIN verification failed.");
        }

        return MonoKycVerificationResult.Succeeded(
            TryGetProviderReference(document.RootElement));
    }

    private static bool ProviderResponseIndicatesFailure(JsonElement root)
    {
        if (TryGetBoolean(root, "success", out var success) && !success)
        {
            return true;
        }

        if (!TryGetString(root, "status", out var status))
        {
            return false;
        }

        return status.Equals("failed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("error", StringComparison.OrdinalIgnoreCase)
            || status.Equals("rejected", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetProviderReference(JsonElement root)
    {
        if (TryGetString(root, "reference", out var reference))
        {
            return reference;
        }

        if (TryGetString(root, "id", out var id))
        {
            return id;
        }

        if (TryGetString(root, "provider_reference", out var providerReference))
        {
            return providerReference;
        }

        if (root.TryGetProperty("data", out var data))
        {
            if (TryGetString(data, "reference", out var dataReference))
            {
                return dataReference;
            }

            if (TryGetString(data, "id", out var dataId))
            {
                return dataId;
            }
        }

        return null;
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = "";

        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? "";

        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetBoolean(
        JsonElement element,
        string propertyName,
        out bool value)
    {
        value = false;

        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            && TryReadBoolean(property, out value);
    }

    private static bool TryReadBoolean(JsonElement property, out bool value)
    {
        value = property.GetBoolean();

        return true;
    }

    private sealed class MonoMashupLookupRequest
    {
        [JsonPropertyName("nin")]
        public string Nin { get; set; } = "";

        [JsonPropertyName("bvn")]
        public string Bvn { get; set; } = "";

        [JsonPropertyName("date_of_birth")]
        public string DateOfBirth { get; set; } = "";
    }
}

public class MonoKycVerificationResult
{
    private MonoKycVerificationResult(
        bool isVerified,
        string? providerReference,
        string? errorMessage)
    {
        IsVerified = isVerified;
        ProviderReference = providerReference;
        ErrorMessage = errorMessage;
    }

    public bool IsVerified { get; }
    public string? ProviderReference { get; }
    public string? ErrorMessage { get; }

    public static MonoKycVerificationResult Succeeded(string? providerReference)
    {
        return new MonoKycVerificationResult(true, providerReference, null);
    }

    public static MonoKycVerificationResult Failed(string errorMessage)
    {
        return new MonoKycVerificationResult(false, null, errorMessage);
    }
}
