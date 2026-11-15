using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.API.Contracts
{
    public record CreateSavingsGoalRequest(string Name, decimal TargetAmount, DateOnly? TargetDate, string Currency);

    public record UpdateSavingsGoalRequest(string Name, decimal TargetAmount, DateOnly? TargetDate);

    public record SavingsMovementRequest(int AccountId, decimal Amount);

    public record CloseSavingsGoalRequest(int AccountId);

    public record SavingsGoalResponse(
        int Id,
        string Name,
        decimal TargetAmount,
        DateOnly? TargetDate,
        SavingsGoalStatus Status,
        int AccountId,
        string? AccountNumber,
        string Currency,
        decimal SavedAmount,
        decimal Progress,
        decimal? MonthlyNeeded,
        DateTime CreatedAt,
        DateTime? AchievedAt,
        DateTime? ClosedAt);

    public static class SavingsMappings
    {
        public static SavingsGoalResponse ToResponse(this SavingsGoal goal, DateOnly today)
        {
            var saved = goal.Account?.Balance ?? 0m;
            var remaining = Math.Max(0, goal.TargetAmount - saved);
            decimal? monthly = null;
            if (goal.Status == SavingsGoalStatus.Aktivan && goal.TargetDate.HasValue && remaining > 0)
            {
                var months = Math.Max(1, (goal.TargetDate.Value.Year - today.Year) * 12 + goal.TargetDate.Value.Month - today.Month);
                monthly = decimal.Ceiling(remaining / months * 100m) / 100m;
            }
            return new SavingsGoalResponse(goal.Id, goal.Name, goal.TargetAmount, goal.TargetDate, goal.Status, goal.AccountId,
                goal.Account?.AccountNumber, goal.Account?.Currency ?? "RSD", saved,
                Math.Min(100m, decimal.Round(saved / goal.TargetAmount * 100m, 1)), monthly, goal.CreatedAt, goal.AchievedAt, goal.ClosedAt);
        }
    }
}
