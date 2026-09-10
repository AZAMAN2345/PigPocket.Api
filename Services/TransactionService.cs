using MongoDB.Bson;
using MongoDB.Driver;
using PigPocket.Api.DTOs.Transactions;
using PigPocket.Api.Models;
using PigPocket.Api.Models.Enums;
using PigPocket.Api.Services.Banking;

namespace PigPocket.Api.Services;

public class TransactionService
{
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly CategoryService _categoryService;
    private readonly MerchantService _merchantService;

    public TransactionService(
        IMongoDatabase database,
        CategoryService categoryService,
        MerchantService merchantService)
    {
        _transactions = database.GetCollection<Transaction>("transactions");
        _categoryService = categoryService;
        _merchantService = merchantService;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Transaction>(
                Builders<Transaction>.IndexKeys
                    .Ascending(transaction => transaction.UserId)
                    .Descending(transaction => transaction.TransactionDate),
                new CreateIndexOptions { Name = "ix_transactions_user_date" }),

            new CreateIndexModel<Transaction>(
                Builders<Transaction>.IndexKeys.Ascending(transaction => transaction.CategoryId),
                new CreateIndexOptions { Name = "ix_transactions_category" }),

            new CreateIndexModel<Transaction>(
                Builders<Transaction>.IndexKeys
                    .Ascending(transaction => transaction.UserId)
                    .Ascending(transaction => transaction.CountsAsSpending)
                    .Descending(transaction => transaction.TransactionDate),
                new CreateIndexOptions { Name = "ix_transactions_user_spending_date" }),

            new CreateIndexModel<Transaction>(
                Builders<Transaction>.IndexKeys
                    .Ascending(transaction => transaction.UserId)
                    .Ascending(transaction => transaction.CategoryId)
                    .Descending(transaction => transaction.TransactionDate),
                new CreateIndexOptions { Name = "ix_transactions_user_category_date" }),

            new CreateIndexModel<Transaction>(
                Builders<Transaction>.IndexKeys
                    .Ascending(transaction => transaction.UserId)
                    .Ascending(transaction => transaction.Merchant)
                    .Descending(transaction => transaction.TransactionDate),
                new CreateIndexOptions { Name = "ix_transactions_user_merchant_date" }),

            new CreateIndexModel<Transaction>(
                Builders<Transaction>.IndexKeys.Ascending(transaction => transaction.BankAccountId),
                new CreateIndexOptions { Name = "ix_transactions_bank_account" }),

            new CreateIndexModel<Transaction>(
                Builders<Transaction>.IndexKeys.Ascending(transaction => transaction.ProviderTransactionId),
                new CreateIndexOptions { Name = "ix_transactions_provider_transaction_id" }),

            new CreateIndexModel<Transaction>(
                Builders<Transaction>.IndexKeys
                    .Ascending(transaction => transaction.UserId)
                    .Ascending(transaction => transaction.BankAccountId)
                    .Ascending(transaction => transaction.ProviderTransactionId),
                new CreateIndexOptions<Transaction>
                {
                    Unique = true,
                    Name = "ux_transactions_user_account_provider_transaction",
                    PartialFilterExpression = new BsonDocument("ProviderTransactionId", new BsonDocument("$type", "string"))
                })
        };

        await _transactions.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetAllAsync(
        Guid userId,
        TransactionQuery query,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildUserTransactionFilter(userId, query);

        return await _transactions
            .Find(filter)
            .SortByDescending(transaction => transaction.TransactionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Transaction?> GetByIdAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _transactions
            .Find(transaction => transaction.UserId == userId && transaction.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TransactionServiceResult<Transaction>> CreateManualAsync(
        Guid userId,
        CreateTransactionDto dto,
        CancellationToken cancellationToken = default)
    {
        var category = await _categoryService.ResolveForManualAsync(
            dto.CategoryId,
            dto.Type,
            cancellationToken);

        var merchant = _merchantService.ResolveManualMerchant(
            dto.Merchant,
            dto.Description);

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Merchant = merchant.Merchant,
            Description = dto.Description?.Trim() ?? "",
            Amount = Math.Abs(dto.Amount),
            Type = dto.Type,
            CategoryId = category.CategoryId,
            Source = TransactionSource.Manual,
            TransactionDate = dto.TransactionDate ?? DateTime.UtcNow,
            IsInternalTransfer = false,
            CountsAsSpending = dto.Type == TransactionType.Debit,
            CategoryConfidence = category.Confidence,
            CreatedAt = DateTime.UtcNow
        };

        await _transactions.InsertOneAsync(transaction, cancellationToken: cancellationToken);

        return TransactionServiceResult<Transaction>.Succeeded(transaction);
    }

    public async Task<Transaction?> UpdateAsync(
        Guid userId,
        Guid id,
        UpdateTransactionDto dto,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<UpdateDefinition<Transaction>>();

        if (dto.Merchant is not null)
        {
            updates.Add(Builders<Transaction>.Update.Set(transaction => transaction.Merchant, dto.Merchant.Trim()));
        }

        if (dto.Description is not null)
        {
            updates.Add(Builders<Transaction>.Update.Set(transaction => transaction.Description, dto.Description.Trim()));
        }

        if (dto.CategoryId.HasValue)
        {
            updates.Add(Builders<Transaction>.Update.Set(transaction => transaction.CategoryId, dto.CategoryId.Value));
            updates.Add(Builders<Transaction>.Update.Set(transaction => transaction.CategoryConfidence, 1m));
        }

        if (dto.TransactionDate.HasValue)
        {
            updates.Add(Builders<Transaction>.Update.Set(transaction => transaction.TransactionDate, dto.TransactionDate.Value));
        }

        if (updates.Count == 0)
        {
            return await GetByIdAsync(userId, id, cancellationToken);
        }

        return await _transactions
            .FindOneAndUpdateAsync(
                transaction => transaction.UserId == userId && transaction.Id == id,
                Builders<Transaction>.Update.Combine(updates),
                new FindOneAndUpdateOptions<Transaction>
                {
                    ReturnDocument = ReturnDocument.After
                },
                cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _transactions.DeleteOneAsync(
            transaction => transaction.UserId == userId && transaction.Id == id,
            cancellationToken);

        return result.DeletedCount == 1;
    }

    public async Task<TransactionImportResult> ImportMonoTransactionsAsync(
        Guid userId,
        BankAccount bankAccount,
        IReadOnlyList<MonoTransactionItem> monoTransactions,
        CancellationToken cancellationToken = default)
    {
        var imported = 0;
        var skippedDuplicates = 0;

        foreach (var monoTransaction in monoTransactions)
        {
            var providerTransactionId = ResolveProviderTransactionId(
                bankAccount.ProviderAccountId ?? bankAccount.Id.ToString(),
                monoTransaction);

            var duplicateExists = await _transactions
                .Find(transaction =>
                    transaction.UserId == userId
                    && transaction.BankAccountId == bankAccount.Id
                    && transaction.ProviderTransactionId == providerTransactionId)
                .AnyAsync(cancellationToken);

            if (duplicateExists)
            {
                skippedDuplicates++;
                continue;
            }

            var type = ResolveTransactionType(monoTransaction.Type);
            var merchant = _merchantService.ResolveMerchant(monoTransaction);
            var category = await _categoryService.ResolveForImportedAsync(
                monoTransaction,
                merchant,
                cancellationToken);

            var isInternalTransfer = await IsLikelyInternalTransferAsync(
                userId,
                bankAccount.Id,
                monoTransaction.Amount,
                type,
                monoTransaction.Narration,
                monoTransaction.Date,
                cancellationToken);

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BankAccountId = bankAccount.Id,
                Merchant = merchant.Merchant,
                Description = monoTransaction.Narration ?? "",
                Amount = Math.Abs(monoTransaction.Amount),
                Type = type,
                CategoryId = category.CategoryId,
                Source = TransactionSource.BankSync,
                TransactionDate = monoTransaction.Date,
                IsInternalTransfer = isInternalTransfer,
                CountsAsSpending = type == TransactionType.Debit && !isInternalTransfer,
                CategoryConfidence = isInternalTransfer ? 0.8m : category.Confidence,
                ProviderTransactionId = providerTransactionId,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _transactions.InsertOneAsync(transaction, cancellationToken: cancellationToken);
                imported++;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                skippedDuplicates++;
            }
        }

        return new TransactionImportResult(imported, skippedDuplicates);
    }

    private async Task<bool> IsLikelyInternalTransferAsync(
        Guid userId,
        Guid currentBankAccountId,
        decimal amount,
        TransactionType type,
        string? narration,
        DateTime transactionDate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(narration)
            || (!narration.Contains("transfer", StringComparison.OrdinalIgnoreCase)
                && !narration.Contains("nip", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var oppositeType = type == TransactionType.Debit
            ? TransactionType.Credit
            : TransactionType.Debit;

        var startDate = transactionDate.AddDays(-2);
        var endDate = transactionDate.AddDays(2);

        return await _transactions
            .Find(transaction =>
                transaction.UserId == userId
                && transaction.BankAccountId != null
                && transaction.BankAccountId != currentBankAccountId
                && transaction.Type == oppositeType
                && transaction.Amount == Math.Abs(amount)
                && transaction.TransactionDate >= startDate
                && transaction.TransactionDate <= endDate)
            .AnyAsync(cancellationToken);
    }

    private static FilterDefinition<Transaction> BuildUserTransactionFilter(
        Guid userId,
        TransactionQuery query)
    {
        var filters = new List<FilterDefinition<Transaction>>
        {
            Builders<Transaction>.Filter.Eq(transaction => transaction.UserId, userId)
        };

        if (query.Type.HasValue)
        {
            filters.Add(Builders<Transaction>.Filter.Eq(transaction => transaction.Type, query.Type.Value));
        }

        if (query.CategoryId.HasValue)
        {
            filters.Add(Builders<Transaction>.Filter.Eq(transaction => transaction.CategoryId, query.CategoryId.Value));
        }

        if (query.BankAccountId.HasValue)
        {
            filters.Add(Builders<Transaction>.Filter.Eq(transaction => transaction.BankAccountId, query.BankAccountId.Value));
        }

        if (query.Source.HasValue)
        {
            filters.Add(Builders<Transaction>.Filter.Eq(transaction => transaction.Source, query.Source.Value));
        }

        if (query.StartDate.HasValue)
        {
            filters.Add(Builders<Transaction>.Filter.Gte(transaction => transaction.TransactionDate, query.StartDate.Value));
        }

        if (query.EndDate.HasValue)
        {
            filters.Add(Builders<Transaction>.Filter.Lte(transaction => transaction.TransactionDate, query.EndDate.Value));
        }

        return Builders<Transaction>.Filter.And(filters);
    }

    private static TransactionType ResolveTransactionType(string? providerType)
    {
        return providerType?.Equals("credit", StringComparison.OrdinalIgnoreCase) == true
            ? TransactionType.Credit
            : TransactionType.Debit;
    }

    private static string ResolveProviderTransactionId(
        string accountIdentity,
        MonoTransactionItem monoTransaction)
    {
        if (!string.IsNullOrWhiteSpace(monoTransaction.ProviderTransactionId))
        {
            return monoTransaction.ProviderTransactionId;
        }

        return string.Join(
            "|",
            "mono-fingerprint",
            accountIdentity,
            Math.Abs(monoTransaction.Amount).ToString("0.00"),
            monoTransaction.Date.ToUniversalTime().ToString("O"),
            monoTransaction.Type?.Trim().ToLowerInvariant() ?? "",
            monoTransaction.Narration?.Trim().ToLowerInvariant() ?? "");
    }
}

public record TransactionQuery(
    TransactionType? Type,
    Guid? CategoryId,
    Guid? BankAccountId,
    DateTime? StartDate,
    DateTime? EndDate,
    TransactionSource? Source);

public record TransactionImportResult(
    int Imported,
    int SkippedDuplicates);

public class TransactionServiceResult<T>
{
    private TransactionServiceResult(bool isSuccess, T? value, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorMessage { get; }

    public static TransactionServiceResult<T> Succeeded(T value)
    {
        return new TransactionServiceResult<T>(true, value, null);
    }

    public static TransactionServiceResult<T> Failed(string errorMessage)
    {
        return new TransactionServiceResult<T>(false, default, errorMessage);
    }
}
