using MongoDB.Driver;
using PigPocket.Api.DTOs.Budgets;
using PigPocket.Api.Models;
using PigPocket.Api.Models.Enums;

namespace PigPocket.Api.Services;

public class BudgetService
{
    private const decimal PercentageTolerance = 0.01m;

    private readonly IMongoCollection<BudgetPlan> _budgetPlans;
    private readonly IMongoCollection<Transaction> _transactions;
    private readonly CategoryService _categoryService;
    private readonly PeriodService _periodService;

    public BudgetService(
        IMongoDatabase database,
        CategoryService categoryService,
        PeriodService periodService)
    {
        _budgetPlans = database.GetCollection<BudgetPlan>("budgetPlans");
        _transactions = database.GetCollection<Transaction>("transactions");
        _categoryService = categoryService;
        _periodService = periodService;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _budgetPlans.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BudgetPlan>(
                    Builders<BudgetPlan>.IndexKeys.Ascending(plan => plan.UserId),
                    new CreateIndexOptions { Name = "ix_budget_plans_user" }),

                new CreateIndexModel<BudgetPlan>(
                    Builders<BudgetPlan>.IndexKeys
                        .Ascending(plan => plan.UserId)
                        .Ascending(plan => plan.StartDate)
                        .Ascending(plan => plan.EndDate),
                    new CreateIndexOptions { Name = "ix_budget_plans_user_dates" })
            ],
            cancellationToken);
    }

    public async Task<BudgetServiceResult<BudgetPlan>> CreateBudgetAsync(
        Guid userId,
        CreateBudgetPlanDto dto,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAndBuildAllocationsAsync(
            dto.InputMode,
            dto.TotalBudgetAmount,
            dto.Allocations,
            cancellationToken);

        if (!validation.IsSuccess)
        {
            return BudgetServiceResult<BudgetPlan>.Failed(validation.ErrorMessage);
        }

        var range = ResolveBudgetRange(dto.Period, dto.StartDate);
        var now = DateTime.UtcNow;
        var budgetPlan = new BudgetPlan
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name.Trim(),
            Period = dto.Period,
            InputMode = dto.InputMode,
            TotalBudgetAmount = dto.TotalBudgetAmount,
            StartDate = range.StartDate,
            EndDate = range.EndDate,
            Allocations = validation.Value!,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _budgetPlans.InsertOneAsync(budgetPlan, cancellationToken: cancellationToken);

        return BudgetServiceResult<BudgetPlan>.Succeeded(budgetPlan);
    }

    public async Task<IReadOnlyList<BudgetPlan>> GetBudgetsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _budgetPlans
            .Find(plan => plan.UserId == userId)
            .SortByDescending(plan => plan.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<BudgetPlan?> GetCurrentBudgetAsync(
        Guid userId,
        BudgetPeriod? period = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var builder = Builders<BudgetPlan>.Filter;
        var filter = builder.Eq(plan => plan.UserId, userId)
            & builder.Lte(plan => plan.StartDate, now)
            & builder.Gte(plan => plan.EndDate, now);

        if (period.HasValue)
        {
            filter &= builder.Eq(plan => plan.Period, period.Value);
        }

        return await _budgetPlans
            .Find(filter)
            .SortByDescending(plan => plan.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BudgetPlan?> GetBudgetByIdAsync(
        Guid userId,
        Guid budgetId,
        CancellationToken cancellationToken = default)
    {
        return await _budgetPlans
            .Find(plan => plan.UserId == userId && plan.Id == budgetId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BudgetServiceResult<BudgetPlan>> UpdateBudgetAsync(
        Guid userId,
        Guid budgetId,
        UpdateBudgetPlanDto dto,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetBudgetByIdAsync(userId, budgetId, cancellationToken);

        if (existing is null)
        {
            return BudgetServiceResult<BudgetPlan>.Failed("Budget was not found.");
        }

        var validation = await ValidateAndBuildAllocationsAsync(
            dto.InputMode,
            dto.TotalBudgetAmount,
            dto.Allocations,
            cancellationToken);

        if (!validation.IsSuccess)
        {
            return BudgetServiceResult<BudgetPlan>.Failed(validation.ErrorMessage);
        }

        var range = ResolveBudgetRange(dto.Period, dto.StartDate);
        existing.Name = dto.Name.Trim();
        existing.Period = dto.Period;
        existing.InputMode = dto.InputMode;
        existing.TotalBudgetAmount = dto.TotalBudgetAmount;
        existing.StartDate = range.StartDate;
        existing.EndDate = range.EndDate;
        existing.Allocations = validation.Value!;
        existing.UpdatedAt = DateTime.UtcNow;

        await _budgetPlans.ReplaceOneAsync(
            plan => plan.UserId == userId && plan.Id == budgetId,
            existing,
            cancellationToken: cancellationToken);

        return BudgetServiceResult<BudgetPlan>.Succeeded(existing);
    }

    public async Task<bool> DeleteBudgetAsync(
        Guid userId,
        Guid budgetId,
        CancellationToken cancellationToken = default)
    {
        var result = await _budgetPlans.DeleteOneAsync(
            plan => plan.UserId == userId && plan.Id == budgetId,
            cancellationToken);

        return result.DeletedCount == 1;
    }

    public async Task<BudgetServiceResult<BudgetStatusDto>> GetBudgetStatusAsync(
        Guid userId,
        Guid budgetId,
        CancellationToken cancellationToken = default)
    {
        var budget = await GetBudgetByIdAsync(userId, budgetId, cancellationToken);

        if (budget is null)
        {
            return BudgetServiceResult<BudgetStatusDto>.Failed("Budget was not found.");
        }

        var categoryIds = budget.Allocations.Select(allocation => allocation.CategoryId).ToArray();
        var spending = await _transactions
            .Find(transaction =>
                transaction.UserId == userId
                && transaction.CountsAsSpending
                && transaction.CategoryId != null
                && categoryIds.Contains(transaction.CategoryId.Value)
                && transaction.TransactionDate >= budget.StartDate
                && transaction.TransactionDate <= budget.EndDate)
            .ToListAsync(cancellationToken);

        var spentByCategory = spending
            .GroupBy(transaction => transaction.CategoryId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(transaction => transaction.Amount));

        var allocationStatuses = new List<CategoryBudgetStatusDto>();

        foreach (var allocation in budget.Allocations)
        {
            var category = await _categoryService.GetByIdAsync(allocation.CategoryId, cancellationToken);
            var spent = spentByCategory.GetValueOrDefault(allocation.CategoryId);
            var overBudgetAmount = Math.Max(0, spent - allocation.AllocatedAmount);
            var remaining = Math.Max(0, allocation.AllocatedAmount - spent);

            allocationStatuses.Add(new CategoryBudgetStatusDto
            {
                CategoryId = allocation.CategoryId,
                Category = category?.Name ?? "Unknown Category",
                AllocationPercentage = allocation.Percentage,
                Budget = allocation.AllocatedAmount,
                Spent = spent,
                Remaining = remaining,
                PercentageUsed = allocation.AllocatedAmount <= 0 ? 0 : spent / allocation.AllocatedAmount * 100m,
                IsOverBudget = spent > allocation.AllocatedAmount,
                OverBudgetAmount = overBudgetAmount
            });
        }

        var totalSpent = allocationStatuses.Sum(status => status.Spent);
        var totalOverBudgetAmount = Math.Max(0, totalSpent - budget.TotalBudgetAmount);

        return BudgetServiceResult<BudgetStatusDto>.Succeeded(new BudgetStatusDto
        {
            BudgetId = budget.Id,
            Name = budget.Name,
            Period = budget.Period,
            TotalBudget = budget.TotalBudgetAmount,
            TotalSpent = totalSpent,
            Remaining = Math.Max(0, budget.TotalBudgetAmount - totalSpent),
            PercentageUsed = budget.TotalBudgetAmount <= 0 ? 0 : totalSpent / budget.TotalBudgetAmount * 100m,
            IsOverBudget = totalSpent > budget.TotalBudgetAmount,
            OverBudgetAmount = totalOverBudgetAmount,
            Allocations = allocationStatuses
        });
    }

    private DateRange ResolveBudgetRange(BudgetPeriod period, DateTime startDate)
    {
        return period switch
        {
            BudgetPeriod.Daily => _periodService.GetDailyRange(startDate),
            BudgetPeriod.Weekly => _periodService.GetWeeklyRange(startDate),
            BudgetPeriod.Monthly => _periodService.GetMonthlyRange(startDate),
            _ => _periodService.GetMonthlyRange(startDate)
        };
    }

    private async Task<BudgetServiceResult<List<BudgetAllocation>>> ValidateAndBuildAllocationsAsync(
        BudgetInputMode inputMode,
        decimal totalBudgetAmount,
        List<BudgetAllocationDto> allocationDtos,
        CancellationToken cancellationToken)
    {
        if (allocationDtos.Count == 0)
        {
            return BudgetServiceResult<List<BudgetAllocation>>.Failed("At least one budget allocation is required.");
        }

        if (allocationDtos.Any(allocation => allocation.CategoryId == Guid.Empty))
        {
            return BudgetServiceResult<List<BudgetAllocation>>.Failed("Every allocation must include a valid category.");
        }

        if (allocationDtos.Select(allocation => allocation.CategoryId).Distinct().Count() != allocationDtos.Count)
        {
            return BudgetServiceResult<List<BudgetAllocation>>.Failed("Duplicate category allocations are not allowed.");
        }

        foreach (var allocation in allocationDtos)
        {
            if (await _categoryService.GetByIdAsync(allocation.CategoryId, cancellationToken) is null)
            {
                return BudgetServiceResult<List<BudgetAllocation>>.Failed("One or more categories were not found.");
            }
        }

        return inputMode == BudgetInputMode.Percentage
            ? ValidatePercentageAllocations(totalBudgetAmount, allocationDtos)
            : ValidateAmountAllocations(totalBudgetAmount, allocationDtos);
    }

    private static BudgetServiceResult<List<BudgetAllocation>> ValidatePercentageAllocations(
        decimal totalBudgetAmount,
        List<BudgetAllocationDto> allocationDtos)
    {
        if (allocationDtos.Any(allocation => allocation.Percentage is null or < 0))
        {
            return BudgetServiceResult<List<BudgetAllocation>>.Failed("Percentage allocations must be zero or greater.");
        }

        var totalPercentage = allocationDtos.Sum(allocation => allocation.Percentage!.Value);

        if (Math.Abs(totalPercentage - 100m) > PercentageTolerance)
        {
            return BudgetServiceResult<List<BudgetAllocation>>.Failed("Percentage allocations must total 100%.");
        }

        return BudgetServiceResult<List<BudgetAllocation>>.Succeeded(
            allocationDtos
                .Select(allocation => new BudgetAllocation
                {
                    CategoryId = allocation.CategoryId,
                    Percentage = allocation.Percentage!.Value,
                    AllocatedAmount = totalBudgetAmount * (allocation.Percentage.Value / 100m)
                })
                .ToList());
    }

    private static BudgetServiceResult<List<BudgetAllocation>> ValidateAmountAllocations(
        decimal totalBudgetAmount,
        List<BudgetAllocationDto> allocationDtos)
    {
        if (allocationDtos.Any(allocation => allocation.Amount is null or < 0))
        {
            return BudgetServiceResult<List<BudgetAllocation>>.Failed("Amount allocations must be zero or greater.");
        }

        var totalAllocated = allocationDtos.Sum(allocation => allocation.Amount!.Value);

        if (totalAllocated > totalBudgetAmount)
        {
            return BudgetServiceResult<List<BudgetAllocation>>.Failed("Amount allocations cannot exceed the total budget amount.");
        }

        return BudgetServiceResult<List<BudgetAllocation>>.Succeeded(
            allocationDtos
                .Select(allocation => new BudgetAllocation
                {
                    CategoryId = allocation.CategoryId,
                    AllocatedAmount = allocation.Amount!.Value,
                    Percentage = totalBudgetAmount <= 0 ? 0 : allocation.Amount.Value / totalBudgetAmount * 100m
                })
                .ToList());
    }
}

public class BudgetServiceResult<T>
{
    private BudgetServiceResult(bool isSuccess, T? value, string errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public string ErrorMessage { get; }

    public static BudgetServiceResult<T> Succeeded(T value)
    {
        return new BudgetServiceResult<T>(true, value, "");
    }

    public static BudgetServiceResult<T> Failed(string errorMessage)
    {
        return new BudgetServiceResult<T>(false, default, errorMessage);
    }
}
