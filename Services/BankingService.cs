using System.Security.Cryptography;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PigPocket.Api.DTOs.Banking;
using PigPocket.Api.Models;
using PigPocket.Api.Services.Banking;

namespace PigPocket.Api.Services;

public class BankingService
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<BankAccount> _bankAccounts;
    private readonly IMongoCollection<BankConnectionSession> _connectionSessions;
    private readonly IMongoCollection<MonoWebhookEvent> _webhookEvents;
    private readonly MonoService _monoService;
    private readonly TransactionService _transactionService;

    public BankingService(
        IMongoDatabase database,
        MonoService monoService,
        TransactionService transactionService)
    {
        _users = database.GetCollection<User>("Users");
        _bankAccounts = database.GetCollection<BankAccount>("bankAccounts");
        _connectionSessions = database.GetCollection<BankConnectionSession>("bankConnectionSessions");
        _webhookEvents = database.GetCollection<MonoWebhookEvent>("monoWebhookEvents");
        _monoService = monoService;
        _transactionService = transactionService;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _bankAccounts.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BankAccount>(
                    Builders<BankAccount>.IndexKeys.Ascending(account => account.UserId),
                    new CreateIndexOptions { Name = "ix_bank_accounts_user" }),

                new CreateIndexModel<BankAccount>(
                    Builders<BankAccount>.IndexKeys.Ascending(account => account.ProviderAccountId),
                    new CreateIndexOptions { Name = "ix_bank_accounts_provider_account_id" }),

                new CreateIndexModel<BankAccount>(
                    Builders<BankAccount>.IndexKeys
                        .Ascending(account => account.Provider)
                        .Ascending(account => account.ProviderAccountId),
                    new CreateIndexOptions<BankAccount>
                    {
                        Unique = true,
                        Name = "ux_bank_accounts_provider_account",
                        PartialFilterExpression = new BsonDocument("ProviderAccountId", new BsonDocument("$type", "string"))
                    })
            ],
            cancellationToken);

        await _connectionSessions.Indexes.CreateOneAsync(
            new CreateIndexModel<BankConnectionSession>(
                Builders<BankConnectionSession>.IndexKeys.Ascending(session => session.Reference),
                new CreateIndexOptions { Unique = true, Name = "ux_bank_connection_sessions_reference" }),
            cancellationToken: cancellationToken);

        await _webhookEvents.Indexes.CreateOneAsync(
            new CreateIndexModel<MonoWebhookEvent>(
                Builders<MonoWebhookEvent>.IndexKeys.Ascending(webhook => webhook.EventId),
                new CreateIndexOptions { Unique = true, Name = "ux_mono_webhook_events_event_id" }),
            cancellationToken: cancellationToken);
    }

    public async Task<BankingResult<BankConnectionResponseDto>> InitiateConnectionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _users
            .Find(existing => existing.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return BankingResult<BankConnectionResponseDto>.Failed(
                BankingError.NotFound,
                "User was not found.");
        }

        var reference = GenerateReference();
        var now = DateTime.UtcNow;
        var session = new BankConnectionSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Reference = reference,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30)
        };

        await _connectionSessions.InsertOneAsync(session, cancellationToken: cancellationToken);

        var linkResult = await _monoService.InitiateAccountLinkAsync(
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Email,
            reference,
            cancellationToken);

        if (!linkResult.IsSuccess || string.IsNullOrWhiteSpace(linkResult.Url))
        {
            return BankingResult<BankConnectionResponseDto>.Failed(
                BankingError.ProviderUnavailable,
                linkResult.ErrorMessage ?? "Unable to initiate bank connection.");
        }

        return BankingResult<BankConnectionResponseDto>.Succeeded(new BankConnectionResponseDto
        {
            Url = linkResult.Url
        });
    }

    public async Task<IReadOnlyList<BankAccount>> GetAccountsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _bankAccounts
            .Find(account => account.UserId == userId)
            .SortByDescending(account => account.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<BankAccount?> GetAccountAsync(
        Guid userId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        return await _bankAccounts
            .Find(account => account.UserId == userId && account.Id == accountId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BankingResult<BankSyncResponse>> SyncAccountAsync(
        Guid userId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var bankAccount = await GetAccountAsync(userId, accountId, cancellationToken);

        if (bankAccount is null)
        {
            return BankingResult<BankSyncResponse>.Failed(
                BankingError.NotFound,
                "Bank account was not found.");
        }

        if (string.IsNullOrWhiteSpace(bankAccount.ProviderAccountId))
        {
            return BankingResult<BankSyncResponse>.Failed(
                BankingError.BadRequest,
                "Bank account is missing its provider account ID.");
        }

        var detailsResult = await _monoService.GetAccountDetailsAsync(
            bankAccount.ProviderAccountId,
            realTime: true,
            cancellationToken);

        if (!detailsResult.IsSuccess || detailsResult.Account is null)
        {
            return BankingResult<BankSyncResponse>.Failed(
                BankingError.ProviderUnavailable,
                detailsResult.ErrorMessage ?? "Unable to fetch account details.");
        }

        await UpdateBankAccountFromMonoAsync(bankAccount, detailsResult.Account, cancellationToken);

        if (!MonoService.IsTransactionDataAvailable(detailsResult.Account))
        {
            return BankingResult<BankSyncResponse>.Succeeded(new BankSyncResponse
            {
                Status = "data_not_ready",
                Message = "Mono account data is not ready for transaction sync yet.",
                DataStatus = detailsResult.Account.DataStatus
            });
        }

        var transactionsResult = await _monoService.GetTransactionsAsync(
            bankAccount.ProviderAccountId,
            cancellationToken,
            realTime: true);

        if (!transactionsResult.IsSuccess)
        {
            return BankingResult<BankSyncResponse>.Failed(
                BankingError.ProviderUnavailable,
                transactionsResult.ErrorMessage ?? "Unable to fetch transactions.");
        }

        var importResult = await _transactionService.ImportMonoTransactionsAsync(
            userId,
            bankAccount,
            transactionsResult.Transactions,
            cancellationToken);

        await _bankAccounts.UpdateOneAsync(
            account => account.Id == bankAccount.Id && account.UserId == userId,
            Builders<BankAccount>.Update.Set(account => account.LastSyncedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

        return BankingResult<BankSyncResponse>.Succeeded(new BankSyncResponse
        {
            Status = "synced",
            Message = "Bank account transactions synced.",
            DataStatus = detailsResult.Account.DataStatus,
            Imported = importResult.Imported,
            SkippedDuplicates = importResult.SkippedDuplicates
        });
    }

    public async Task<BankingResult<WebhookProcessingResponse>> ProcessMonoWebhookAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var eventName = ReadString(payload, "event") ?? "";
        var eventId = ReadString(payload, "event_id")
            ?? ReadString(payload, "id");

        if (!string.IsNullOrWhiteSpace(eventId))
        {
            try
            {
                await _webhookEvents.InsertOneAsync(
                    new MonoWebhookEvent
                    {
                        Id = Guid.NewGuid(),
                        EventId = eventId,
                        Event = eventName,
                        ReceivedAt = DateTime.UtcNow
                    },
                    cancellationToken: cancellationToken);
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return BankingResult<WebhookProcessingResponse>.Succeeded(new WebhookProcessingResponse
                {
                    Status = "duplicate_ignored",
                    Message = "Webhook event was already processed."
                });
            }
        }

        return eventName switch
        {
            "mono.events.account_connected" => await ProcessAccountConnectedAsync(payload, cancellationToken),
            "mono.events.account_updated" => await ProcessAccountUpdatedAsync(payload, cancellationToken),
            _ => BankingResult<WebhookProcessingResponse>.Succeeded(new WebhookProcessingResponse
            {
                Status = "ignored",
                Message = "Webhook event ignored."
            })
        };
    }

    private async Task<BankingResult<WebhookProcessingResponse>> ProcessAccountConnectedAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var data = GetPropertyOrDefault(payload, "data");
        var accountId = ReadString(data, "id")
            ?? ReadString(data, "account");
        var reference = ReadNestedString(data, "meta", "ref");

        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(reference))
        {
            return BankingResult<WebhookProcessingResponse>.Failed(
                BankingError.BadRequest,
                "Webhook payload is missing account ID or connection reference.");
        }

        var session = await _connectionSessions
            .Find(existing => existing.Reference == reference)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null || session.ExpiresAt < DateTime.UtcNow)
        {
            return BankingResult<WebhookProcessingResponse>.Failed(
                BankingError.NotFound,
                "Connection session was not found.");
        }

        var bankAccount = await CreateOrUpdateMonoBankAccountAsync(
            session.UserId,
            accountId,
            cancellationToken);

        await _connectionSessions.UpdateOneAsync(
            existing => existing.Id == session.Id,
            Builders<BankConnectionSession>.Update
                .Set(existing => existing.Completed, true)
                .Set(existing => existing.CompletedAt, DateTime.UtcNow)
                .Set(existing => existing.ProviderAccountId, accountId),
            cancellationToken: cancellationToken);

        return BankingResult<WebhookProcessingResponse>.Succeeded(new WebhookProcessingResponse
        {
            Status = "account_connected",
            Message = "Mono account connected.",
            BankAccountId = bankAccount.Id
        });
    }

    private async Task<BankingResult<WebhookProcessingResponse>> ProcessAccountUpdatedAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var data = GetPropertyOrDefault(payload, "data");
        var accountId = ReadString(data, "id")
            ?? ReadString(data, "account");

        if (string.IsNullOrWhiteSpace(accountId))
        {
            return BankingResult<WebhookProcessingResponse>.Failed(
                BankingError.BadRequest,
                "Webhook payload is missing account ID.");
        }

        var bankAccount = await _bankAccounts
            .Find(account => account.Provider == "Mono" && account.ProviderAccountId == accountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (bankAccount is null)
        {
            return BankingResult<WebhookProcessingResponse>.Failed(
                BankingError.NotFound,
                "Linked bank account was not found.");
        }

        var dataStatus = ReadNestedString(data, "meta", "data_status") ?? "";
        var retrievedData = ReadNestedStringArray(data, "meta", "retrieved_data");
        var transactionsAvailable = dataStatus.Equals("AVAILABLE", StringComparison.OrdinalIgnoreCase)
            || (dataStatus.Equals("PARTIAL", StringComparison.OrdinalIgnoreCase)
                && retrievedData.Any(value => value.Equals("transactions", StringComparison.OrdinalIgnoreCase)));

        if (!transactionsAvailable)
        {
            return BankingResult<WebhookProcessingResponse>.Succeeded(new WebhookProcessingResponse
            {
                Status = "data_not_ready",
                Message = "Mono account data is not ready for transaction sync yet.",
                BankAccountId = bankAccount.Id
            });
        }

        var syncResult = await SyncAccountAsync(
            bankAccount.UserId,
            bankAccount.Id,
            cancellationToken);

        if (!syncResult.IsSuccess)
        {
            return BankingResult<WebhookProcessingResponse>.Failed(
                syncResult.Error,
                syncResult.ErrorMessage ?? "Unable to sync linked account.");
        }

        return BankingResult<WebhookProcessingResponse>.Succeeded(new WebhookProcessingResponse
        {
            Status = "account_synced",
            Message = "Mono account transactions synced.",
            BankAccountId = bankAccount.Id,
            Imported = syncResult.Value?.Imported ?? 0,
            SkippedDuplicates = syncResult.Value?.SkippedDuplicates ?? 0
        });
    }

    private async Task<BankAccount> CreateOrUpdateMonoBankAccountAsync(
        Guid userId,
        string providerAccountId,
        CancellationToken cancellationToken)
    {
        var existingAccount = await _bankAccounts
            .Find(account => account.Provider == "Mono" && account.ProviderAccountId == providerAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        var details = await _monoService.GetAccountDetailsAsync(
            providerAccountId,
            realTime: false,
            cancellationToken);

        var accountDetails = details.Account;
        var bankAccount = existingAccount ?? new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "Mono",
            ProviderAccountId = providerAccountId,
            CreatedAt = DateTime.UtcNow
        };

        if (accountDetails is not null)
        {
            bankAccount.BankName = accountDetails.BankName ?? bankAccount.BankName;
            bankAccount.AccountName = accountDetails.AccountName;
            bankAccount.AccountNumberMasked = MaskAccountNumber(accountDetails.AccountNumber);
            bankAccount.CurrentBalance = accountDetails.Balance;
            bankAccount.Currency = string.IsNullOrWhiteSpace(accountDetails.Currency)
                ? "NGN"
                : accountDetails.Currency;
        }

        if (existingAccount is null)
        {
            await _bankAccounts.InsertOneAsync(bankAccount, cancellationToken: cancellationToken);
        }
        else
        {
            await _bankAccounts.ReplaceOneAsync(
                account => account.Id == bankAccount.Id,
                bankAccount,
                cancellationToken: cancellationToken);
        }

        return bankAccount;
    }

    private async Task UpdateBankAccountFromMonoAsync(
        BankAccount bankAccount,
        MonoAccountDetails accountDetails,
        CancellationToken cancellationToken)
    {
        var update = Builders<BankAccount>.Update
            .Set(account => account.BankName, accountDetails.BankName ?? bankAccount.BankName)
            .Set(account => account.AccountName, accountDetails.AccountName)
            .Set(account => account.AccountNumberMasked, MaskAccountNumber(accountDetails.AccountNumber))
            .Set(account => account.CurrentBalance, accountDetails.Balance)
            .Set(account => account.Currency, string.IsNullOrWhiteSpace(accountDetails.Currency) ? "NGN" : accountDetails.Currency);

        await _bankAccounts.UpdateOneAsync(
            account => account.Id == bankAccount.Id && account.UserId == bankAccount.UserId,
            update,
            cancellationToken: cancellationToken);
    }

    private static string GenerateReference()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private static string MaskAccountNumber(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return "";
        }

        var lastFour = accountNumber.Length <= 4
            ? accountNumber
            : accountNumber[^4..];

        return $"****{lastFour}";
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

    private static string? ReadNestedString(
        JsonElement element,
        string parentProperty,
        string childProperty)
    {
        var parent = GetPropertyOrDefault(element, parentProperty);

        return ReadString(parent, childProperty);
    }

    private static IReadOnlyList<string> ReadNestedStringArray(
        JsonElement element,
        string parentProperty,
        string childProperty)
    {
        var parent = GetPropertyOrDefault(element, parentProperty);

        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(childProperty, out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array
            .EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString() ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }
}

public enum BankingError
{
    None,
    BadRequest,
    Unauthorized,
    NotFound,
    Conflict,
    ProviderUnavailable
}

public class BankingResult<T>
{
    private BankingResult(
        bool isSuccess,
        T? value,
        BankingError error,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public BankingError Error { get; }
    public string? ErrorMessage { get; }

    public static BankingResult<T> Succeeded(T value)
    {
        return new BankingResult<T>(true, value, BankingError.None, null);
    }

    public static BankingResult<T> Failed(
        BankingError error,
        string errorMessage)
    {
        return new BankingResult<T>(false, default, error, errorMessage);
    }
}

public class BankSyncResponse
{
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public string? DataStatus { get; set; }
    public int Imported { get; set; }
    public int SkippedDuplicates { get; set; }
}

public class WebhookProcessingResponse
{
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public Guid? BankAccountId { get; set; }
    public int Imported { get; set; }
    public int SkippedDuplicates { get; set; }
}
