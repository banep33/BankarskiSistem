namespace BankarskiSistem.Core.Entities
{
    public class SpendingCategory
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#B8B0A6";
        public int SortOrder { get; set; }
    }

    public class TransactionCategory
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int TransactionId { get; set; }
        public int CategoryId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    }

    public class CategoryRule
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string CounterpartyAccountNumber { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Budget
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int CategoryId { get; set; }
        public decimal MonthlyLimit { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public SpendingCategory? Category { get; set; }
    }

    public class BudgetAlert
    {
        public int Id { get; set; }
        public int BudgetId { get; set; }
        public string Month { get; set; } = string.Empty;
        public int Threshold { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
