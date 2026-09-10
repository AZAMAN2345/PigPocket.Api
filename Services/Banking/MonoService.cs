using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PigPocket.Api.Configuration;

namespace PigPocket.Api.Services.Banking;

public class MonoService
{
    private readonly HttpClient _httpClient;
    private readonly MonoSettings _settings;

    public MonoService(
        HttpClient httpClient,
        MonoSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public async Task<MonoLinkResult> InitiateAccountLinkAsync(
        string name,
        string email,
        string reference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            return MonoLinkResult.Failed("Mono secret key is not configured.");
        }

        using var request = CreateRequest(HttpMethod.Post, "v2/accounts/initiate");

        request.Content = JsonContent.Create(new MonoInitiateAccountRequest
        {
            Customer = new MonoCustomer
            {
                Name = name,
                Email = email
            },
            Meta = new MonoInitiateMeta
            {
                Reference = reference
            },
            Scope = "auth",
            RedirectUrl = _settings.RedirectUrl
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return MonoLinkResult.Failed("Unable to initiate bank connection.");
        }

        var url = ExtractFirstString(
            responseBody,
            "mono_url",
            "url",
            "link",
            "account_link");

        if (string.IsNullOrWhiteSpace(url))
        {
            return MonoLinkResult.Failed("Mono did not return a connection URL.");
        }

        return MonoLinkResult.Succeeded(url);
    }

    public async Task<MonoAccountDetailsResult> GetAccountDetailsAsync(
        string providerAccountId,
        bool realTime,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            return MonoAccountDetailsResult.Failed("Mono secret key is not configured.");
        }

        using var request = CreateRequest(
            HttpMethod.Get,
            $"v2/accounts/{Uri.EscapeDataString(providerAccountId)}");

        if (realTime)
        {
            request.Headers.Add("x-real-time", "true");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return MonoAccountDetailsResult.Failed("Unable to fetch Mono account details.");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var data = GetPropertyOrSelf(root, "data");
        var account = GetPropertyOrSelf(data, "account");
        var meta = GetPropertyOrDefault(data, "meta");

        return MonoAccountDetailsResult.Succeeded(new MonoAccountDetails
        {
            ProviderAccountId = FirstString(account, "id", "_id") ?? providerAccountId,
            AccountName = FirstString(account, "name", "account_name") ?? "",
            AccountNumber = FirstString(account, "account_number", "number") ?? "",
            Currency = FirstString(account, "currency") ?? "NGN",
            Balance = NormalizeProviderAmount(
                FirstDecimal(account, "balance") ?? 0m,
                FirstString(account, "currency") ?? "NGN"),
            BankName = ExtractInstitutionName(account),
            DataStatus = FirstString(meta, "data_status") ?? "",
            RetrievedData = ExtractStringArray(meta, "retrieved_data"),
            Reference = FirstString(meta, "ref")
        });
    }

    public async Task<MonoTransactionsResult> GetTransactionsAsync(
        string providerAccountId,
        CancellationToken cancellationToken = default,
        bool realTime = false)
    {
        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            return MonoTransactionsResult.Failed("Mono secret key is not configured.");
        }

        var transactions = new List<MonoTransactionItem>();
        var nextUrl = $"v2/accounts/{Uri.EscapeDataString(providerAccountId)}/transactions";

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            using var request = CreateRequest(HttpMethod.Get, nextUrl);

            if (realTime)
            {
                request.Headers.Add("x-real-time", "true");
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return MonoTransactionsResult.Failed("Unable to fetch Mono transactions.");
            }

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var data = GetPropertyOrSelf(root, "data");
            var pageTransactions = GetPropertyOrDefault(data, "transactions");

            if (pageTransactions.ValueKind != JsonValueKind.Array)
            {
                pageTransactions = GetPropertyOrDefault(root, "transactions");
            }

            if (pageTransactions.ValueKind == JsonValueKind.Array)
            {
                foreach (var transaction in pageTransactions.EnumerateArray())
                {
                    transactions.Add(ParseTransaction(transaction));
                }
            }

            nextUrl = ExtractNextPageUrl(root);
        }

        return MonoTransactionsResult.Succeeded(transactions);
    }

    public static bool IsTransactionDataAvailable(MonoAccountDetails account)
    {
        var status = account.DataStatus;
        var retrievedTransactions = account.RetrievedData
            .Any(value => value.Equals("transactions", StringComparison.OrdinalIgnoreCase));

        return status.Equals("AVAILABLE", StringComparison.OrdinalIgnoreCase)
            || (status.Equals("PARTIAL", StringComparison.OrdinalIgnoreCase) && retrievedTransactions);
    }

    public static decimal NormalizeProviderAmount(decimal providerAmount, string? currency)
    {
        // Mono returns Nigerian account amounts in the lowest denomination: kobo.
        // Pig Pocket stores money internally in major currency units, so NGN imports become naira.
        return string.Equals(currency, "NGN", StringComparison.OrdinalIgnoreCase)
            ? providerAmount / 100m
            : providerAmount / 100m;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("mono-sec-key", _settings.SecretKey);

        return request;
    }

    private static MonoTransactionItem ParseTransaction(JsonElement element)
    {
        var currency = FirstString(element, "currency") ?? "NGN";

        return new MonoTransactionItem
        {
            ProviderTransactionId = FirstString(element, "_id", "id"),
            Type = FirstString(element, "type"),
            Amount = NormalizeProviderAmount(FirstDecimal(element, "amount") ?? 0m, currency),
            Balance = NormalizeProviderAmount(FirstDecimal(element, "balance") ?? 0m, currency),
            Date = FirstDateTime(element, "date") ?? DateTime.UtcNow,
            Currency = currency,
            Narration = FirstString(element, "narration", "description"),
            Category = FirstString(element, "category"),
            Metadata = ParseMetadata(GetPropertyOrDefault(element, "metadata"))
        };
    }

    private static MonoTransactionMetadata? ParseMetadata(JsonElement metadata)
    {
        if (metadata.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new MonoTransactionMetadata
        {
            Payee = FirstString(metadata, "payee"),
            Merchant = FirstString(metadata, "merchant"),
            Name = FirstString(metadata, "name"),
            Category = FirstString(metadata, "category"),
            Channel = FirstString(metadata, "channel"),
            PaymentMethod = FirstString(metadata, "payment_method"),
            Location = FirstString(metadata, "location"),
            Reason = FirstString(metadata, "reason")
        };
    }

    private static string? ExtractNextPageUrl(JsonElement root)
    {
        var data = GetPropertyOrSelf(root, "data");
        var paging = GetPropertyOrDefault(data, "paging");
        var next = ReadNextLink(paging)
            ?? ReadNextLink(data)
            ?? ReadNextLink(root);

        if (string.IsNullOrWhiteSpace(next))
        {
            return null;
        }

        if (Uri.TryCreate(next, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.PathAndQuery.TrimStart('/');
        }

        return next.TrimStart('/');
    }

    private static string? ExtractInstitutionName(JsonElement account)
    {
        var institution = GetPropertyOrDefault(account, "institution");

        return FirstString(institution, "name")
            ?? FirstString(account, "bank_name", "institution_name");
    }

    private static string? ReadNextLink(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("next", out var next))
        {
            return null;
        }

        if (next.ValueKind == JsonValueKind.String)
        {
            return next.GetString();
        }

        if (next.ValueKind == JsonValueKind.Object)
        {
            return FirstString(next, "url", "href", "link");
        }

        return null;
    }

    private static JsonElement GetPropertyOrSelf(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property))
        {
            return property;
        }

        return element;
    }

    private static JsonElement GetPropertyOrDefault(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property))
        {
            return property;
        }

        return default;
    }

    private static string? FirstString(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static decimal? FirstDecimal(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number
                && property.TryGetDecimal(out var decimalValue))
            {
                return decimalValue;
            }

            if (property.ValueKind == JsonValueKind.String
                && decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static DateTime? FirstDateTime(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                && DateTime.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date))
            {
                return date;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static string? ExtractFirstString(string responseBody, params string[] propertyNames)
    {
        using var document = JsonDocument.Parse(responseBody);

        return FindFirstString(document.RootElement, propertyNames);
    }

    private static string? FindFirstString(JsonElement element, IReadOnlyCollection<string> propertyNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (propertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }

                var nested = FindFirstString(property.Value, propertyNames);

                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindFirstString(item, propertyNames);

                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private sealed class MonoInitiateAccountRequest
    {
        [JsonPropertyName("customer")]
        public MonoCustomer Customer { get; set; } = new();

        [JsonPropertyName("meta")]
        public MonoInitiateMeta Meta { get; set; } = new();

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = "";

        [JsonPropertyName("redirect_url")]
        public string RedirectUrl { get; set; } = "";
    }

    private sealed class MonoCustomer
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "";
    }

    private sealed class MonoInitiateMeta
    {
        [JsonPropertyName("ref")]
        public string Reference { get; set; } = "";
    }
}

public class MonoLinkResult
{
    private MonoLinkResult(bool isSuccess, string? url, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Url = url;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public string? Url { get; }
    public string? ErrorMessage { get; }

    public static MonoLinkResult Succeeded(string url)
    {
        return new MonoLinkResult(true, url, null);
    }

    public static MonoLinkResult Failed(string errorMessage)
    {
        return new MonoLinkResult(false, null, errorMessage);
    }
}

public class MonoAccountDetailsResult
{
    private MonoAccountDetailsResult(bool isSuccess, MonoAccountDetails? account, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Account = account;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public MonoAccountDetails? Account { get; }
    public string? ErrorMessage { get; }

    public static MonoAccountDetailsResult Succeeded(MonoAccountDetails account)
    {
        return new MonoAccountDetailsResult(true, account, null);
    }

    public static MonoAccountDetailsResult Failed(string errorMessage)
    {
        return new MonoAccountDetailsResult(false, null, errorMessage);
    }
}

public class MonoTransactionsResult
{
    private MonoTransactionsResult(bool isSuccess, IReadOnlyList<MonoTransactionItem> transactions, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Transactions = transactions;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public IReadOnlyList<MonoTransactionItem> Transactions { get; }
    public string? ErrorMessage { get; }

    public static MonoTransactionsResult Succeeded(IReadOnlyList<MonoTransactionItem> transactions)
    {
        return new MonoTransactionsResult(true, transactions, null);
    }

    public static MonoTransactionsResult Failed(string errorMessage)
    {
        return new MonoTransactionsResult(false, [], errorMessage);
    }
}

public class MonoAccountDetails
{
    public string ProviderAccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string Currency { get; set; } = "NGN";
    public decimal Balance { get; set; }
    public string? BankName { get; set; }
    public string DataStatus { get; set; } = "";
    public IReadOnlyList<string> RetrievedData { get; set; } = [];
    public string? Reference { get; set; }
}

public class MonoTransactionItem
{
    public string? ProviderTransactionId { get; set; }
    public string? Type { get; set; }
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public DateTime Date { get; set; }
    public string Currency { get; set; } = "NGN";
    public string? Narration { get; set; }
    public string? Category { get; set; }
    public MonoTransactionMetadata? Metadata { get; set; }
}

public class MonoTransactionMetadata
{
    public string? Payee { get; set; }
    public string? Merchant { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Channel { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Location { get; set; }
    public string? Reason { get; set; }
}
