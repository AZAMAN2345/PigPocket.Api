using MongoDB.Driver;
using PigPocket.Api.DTOs.Goals;
using PigPocket.Api.Models;

namespace PigPocket.Api.Services;

public class SavingsGoalService
{
    private readonly IMongoCollection<SavingsGoal> _goals;
    private readonly IMongoCollection<GoalContribution> _contributions;

    public SavingsGoalService(IMongoDatabase database)
    {
        _goals = database.GetCollection<SavingsGoal>("savingsGoals");
        _contributions = database.GetCollection<GoalContribution>("goalContributions");
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await _goals.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<SavingsGoal>(
                    Builders<SavingsGoal>.IndexKeys.Ascending(goal => goal.UserId),
                    new CreateIndexOptions { Name = "ix_savings_goals_user" }),

                new CreateIndexModel<SavingsGoal>(
                    Builders<SavingsGoal>.IndexKeys
                        .Ascending(goal => goal.UserId)
                        .Ascending(goal => goal.IsCompleted),
                    new CreateIndexOptions { Name = "ix_savings_goals_user_completed" })
            ],
            cancellationToken);

        await _contributions.Indexes.CreateOneAsync(
            new CreateIndexModel<GoalContribution>(
                Builders<GoalContribution>.IndexKeys
                    .Ascending(contribution => contribution.UserId)
                    .Ascending(contribution => contribution.SavingsGoalId)
                    .Descending(contribution => contribution.ContributionDate),
                new CreateIndexOptions { Name = "ix_goal_contributions_user_goal_date" }),
            cancellationToken: cancellationToken);
    }

    public async Task<SavingsGoalResponseDto> CreateGoalAsync(
        Guid userId,
        CreateSavingsGoalDto dto,
        CancellationToken cancellationToken = default)
    {
        var goal = new SavingsGoal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name.Trim(),
            TargetAmount = dto.TargetAmount,
            CurrentAmount = 0,
            TargetDate = dto.TargetDate,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        await _goals.InsertOneAsync(goal, cancellationToken: cancellationToken);

        return ToResponse(goal);
    }

    public async Task<IReadOnlyList<SavingsGoalResponseDto>> GetGoalsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var goals = await _goals
            .Find(goal => goal.UserId == userId)
            .SortByDescending(goal => goal.CreatedAt)
            .ToListAsync(cancellationToken);

        return goals.Select(ToResponse).ToList();
    }

    public async Task<SavingsGoalResponseDto?> GetGoalAsync(
        Guid userId,
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        var goal = await FindGoalAsync(userId, goalId, cancellationToken);

        return goal is null ? null : ToResponse(goal);
    }

    public async Task<SavingsGoalResponseDto?> UpdateGoalAsync(
        Guid userId,
        Guid goalId,
        UpdateSavingsGoalDto dto,
        CancellationToken cancellationToken = default)
    {
        var goal = await FindGoalAsync(userId, goalId, cancellationToken);

        if (goal is null)
        {
            return null;
        }

        goal.Name = dto.Name.Trim();
        goal.TargetAmount = dto.TargetAmount;
        goal.TargetDate = dto.TargetDate;
        goal.IsCompleted = goal.CurrentAmount >= goal.TargetAmount;

        await _goals.ReplaceOneAsync(
            existing => existing.UserId == userId && existing.Id == goalId,
            goal,
            cancellationToken: cancellationToken);

        return ToResponse(goal);
    }

    public async Task<bool> DeleteGoalAsync(
        Guid userId,
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        var result = await _goals.DeleteOneAsync(
            goal => goal.UserId == userId && goal.Id == goalId,
            cancellationToken);

        if (result.DeletedCount != 1)
        {
            return false;
        }

        await _contributions.DeleteManyAsync(
            contribution => contribution.UserId == userId && contribution.SavingsGoalId == goalId,
            cancellationToken);

        return true;
    }

    public async Task<SavingsGoalResponseDto?> AddContributionAsync(
        Guid userId,
        Guid goalId,
        AddGoalContributionDto dto,
        CancellationToken cancellationToken = default)
    {
        var goal = await FindGoalAsync(userId, goalId, cancellationToken);

        if (goal is null)
        {
            return null;
        }

        var contribution = new GoalContribution
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SavingsGoalId = goalId,
            Amount = dto.Amount,
            ContributionDate = dto.ContributionDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _contributions.InsertOneAsync(contribution, cancellationToken: cancellationToken);

        goal.CurrentAmount += dto.Amount;
        goal.IsCompleted = goal.CurrentAmount >= goal.TargetAmount;

        await _goals.ReplaceOneAsync(
            existing => existing.UserId == userId && existing.Id == goalId,
            goal,
            cancellationToken: cancellationToken);

        return ToResponse(goal);
    }

    public async Task<IReadOnlyList<GoalContribution>> GetContributionsAsync(
        Guid userId,
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        if (await FindGoalAsync(userId, goalId, cancellationToken) is null)
        {
            return [];
        }

        return await _contributions
            .Find(contribution => contribution.UserId == userId && contribution.SavingsGoalId == goalId)
            .SortByDescending(contribution => contribution.ContributionDate)
            .ToListAsync(cancellationToken);
    }

    private async Task<SavingsGoal?> FindGoalAsync(
        Guid userId,
        Guid goalId,
        CancellationToken cancellationToken)
    {
        return await _goals
            .Find(goal => goal.UserId == userId && goal.Id == goalId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static SavingsGoalResponseDto ToResponse(SavingsGoal goal)
    {
        return new SavingsGoalResponseDto
        {
            Id = goal.Id,
            Name = goal.Name,
            TargetAmount = goal.TargetAmount,
            CurrentAmount = goal.CurrentAmount,
            TargetDate = goal.TargetDate,
            IsCompleted = goal.IsCompleted,
            PercentageComplete = goal.TargetAmount <= 0 ? 0 : goal.CurrentAmount / goal.TargetAmount * 100m,
            CreatedAt = goal.CreatedAt
        };
    }
}
