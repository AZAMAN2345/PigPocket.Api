using MongoDB.Driver;
using PigPocket.Api.Models;
using PigPocket.Api.Models.Enums;
using PigPocket.Api.Services.Banking;

namespace PigPocket.Api.Services;

public class CategoryService
{
    private static readonly CategorySeed[] DefaultCategories =
    [
        new("Food", "food", false),
        new("Transport", "transport", false),
        new("Groceries", "groceries", false),
        new("Shopping", "shopping", false),
        new("Bills", "bills", false),
        new("Entertainment", "entertainment", false),
        new("Subscriptions", "subscriptions", false),
        new("Electronics", "electronics", false),
        new("Health", "health", false),
        new("Education", "education", false),
        new("Salary", "salary", true),
        new("Savings", "savings", false),
        new("Transfers", "transfers", false),
        new("Bank Charges", "bank-charges", false),
        new("Personal Care", "personal-care", false),
        new("Online Payments", "online-payments", false),
        new("Other", "other", false),
        new("Uncategorized", "uncategorized", false)
    ];

    private static readonly Dictionary<string, string> ProviderCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["food"] = "Food",
        ["restaurant"] = "Food",
        ["restaurants"] = "Food",
        ["groceries"] = "Groceries",
        ["grocery"] = "Groceries",
        ["transport"] = "Transport",
        ["transportation"] = "Transport",
        ["ride_hailing"] = "Transport",
        ["salary"] = "Salary",
        ["income"] = "Salary",
        ["bank_charge"] = "Bank Charges",
        ["bank_charges"] = "Bank Charges",
        ["charges"] = "Bank Charges",
        ["personal_care"] = "Personal Care",
        ["online_payments"] = "Online Payments",
        ["online_payment"] = "Online Payments",
        ["transfer"] = "Transfers",
        ["transfers"] = "Transfers",
        ["subscription"] = "Subscriptions",
        ["subscriptions"] = "Subscriptions",
        ["utilities"] = "Bills",
        ["bills"] = "Bills",
        ["education"] = "Education",
        ["health"] = "Health",
        ["medical"] = "Health",
        ["entertainment"] = "Entertainment",
        ["shopping"] = "Shopping",
        ["electronics"] = "Electronics",
        ["savings"] = "Savings"
    };

    private static readonly Dictionary<string, string> MerchantCategoryHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["slot"] = "Electronics",
        ["bolt"] = "Transport",
        ["uber"] = "Transport",
        ["max"] = "Transport",
        ["netflix"] = "Subscriptions",
        ["spotify"] = "Subscriptions",
        ["dstv"] = "Bills",
        ["gotv"] = "Bills",
        ["mtn"] = "Bills",
        ["airtel"] = "Bills",
        ["glo"] = "Bills",
        ["shoprite"] = "Groceries"
    };

    private readonly IMongoCollection<Category> _categories;

    public CategoryService(IMongoDatabase database)
    {
        _categories = database.GetCollection<Category>("categories");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Category>(
                Builders<Category>.IndexKeys.Ascending(category => category.Name),
                new CreateIndexOptions { Unique = true, Name = "ux_categories_name" })
        };

        await _categories.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task EnsureDefaultCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);

        foreach (var category in DefaultCategories)
        {
            var update = Builders<Category>.Update
                .SetOnInsert(existing => existing.Id, Guid.NewGuid())
                .SetOnInsert(existing => existing.Name, category.Name)
                .SetOnInsert(existing => existing.Icon, category.Icon)
                .SetOnInsert(existing => existing.IsIncomeCategory, category.IsIncomeCategory)
                .SetOnInsert(existing => existing.IsSystemCategory, true);

            await _categories.UpdateOneAsync(
                existing => existing.Name == category.Name,
                update,
                new UpdateOptions { IsUpsert = true },
                cancellationToken);
        }
    }

    public async Task<Category?> GetByIdAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await _categories
            .Find(category => category.Id == categoryId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CategoryResolution> ResolveForManualAsync(
        Guid? categoryId,
        TransactionType transactionType,
        CancellationToken cancellationToken = default)
    {
        if (categoryId.HasValue)
        {
            var category = await GetByIdAsync(categoryId.Value, cancellationToken);

            if (category is not null)
            {
                return new CategoryResolution(category.Id, 1m);
            }
        }

        var fallback = transactionType == TransactionType.Credit
            ? "Other"
            : "Uncategorized";

        return await ResolveByNameAsync(fallback, 0.2m, cancellationToken);
    }

    public async Task<CategoryResolution> ResolveForImportedAsync(
        MonoTransactionItem transaction,
        MerchantResolution merchant,
        CancellationToken cancellationToken = default)
    {
        var providerCategory = FirstNonEmpty(
            transaction.Metadata?.Category,
            transaction.Category);

        if (providerCategory is not null
            && ProviderCategoryMap.TryGetValue(Normalize(providerCategory), out var providerMappedCategory))
        {
            return await ResolveByNameAsync(providerMappedCategory, 0.9m, cancellationToken);
        }

        var merchantCategory = ResolveFromMerchant(merchant.Merchant);

        if (merchantCategory is not null)
        {
            return await ResolveByNameAsync(merchantCategory, 0.75m, cancellationToken);
        }

        var narrationCategory = ResolveFromNarration(transaction.Narration);

        if (narrationCategory is not null)
        {
            return await ResolveByNameAsync(narrationCategory, 0.6m, cancellationToken);
        }

        return await ResolveByNameAsync("Uncategorized", 0.2m, cancellationToken);
    }

    private async Task<CategoryResolution> ResolveByNameAsync(
        string categoryName,
        decimal confidence,
        CancellationToken cancellationToken)
    {
        var category = await _categories
            .Find(existing => existing.Name == categoryName)
            .FirstOrDefaultAsync(cancellationToken);

        if (category is not null)
        {
            return new CategoryResolution(category.Id, confidence);
        }

        var uncategorized = await _categories
            .Find(existing => existing.Name == "Uncategorized")
            .FirstOrDefaultAsync(cancellationToken);

        return new CategoryResolution(uncategorized?.Id, 0.2m);
    }

    private static string? ResolveFromMerchant(string merchant)
    {
        foreach (var pair in MerchantCategoryHints)
        {
            if (merchant.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string? ResolveFromNarration(string? narration)
    {
        if (string.IsNullOrWhiteSpace(narration))
        {
            return null;
        }

        var normalized = Normalize(narration);

        if (normalized.Contains("salary", StringComparison.OrdinalIgnoreCase))
        {
            return "Salary";
        }

        if (normalized.Contains("transfer", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("nip", StringComparison.OrdinalIgnoreCase))
        {
            return "Transfers";
        }

        if (normalized.Contains("charge", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("fee", StringComparison.OrdinalIgnoreCase))
        {
            return "Bank Charges";
        }

        return null;
    }

    private static string Normalize(string value)
    {
        return value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private record CategorySeed(
        string Name,
        string Icon,
        bool IsIncomeCategory);
}

public record CategoryResolution(
    Guid? CategoryId,
    decimal Confidence);
