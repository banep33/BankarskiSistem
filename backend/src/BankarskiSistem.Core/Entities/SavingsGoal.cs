using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class SavingsGoal
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public DateOnly? TargetDate { get; set; }
        public SavingsGoalStatus Status { get; set; } = SavingsGoalStatus.Aktivan;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AchievedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        public Account? Account { get; set; }
    }
}
