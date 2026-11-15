namespace BankarskiSistem.Core.Models
{
    public record CategorizedTransaction(
        int TransactionId,
        DateTime Date,
        string Reference,
        string Description,
        string Counterparty,
        string? CounterpartyAccountNumber,
        decimal Amount,
        int CategoryId,
        bool IsExplicit);

    public record CategorySpending(int CategoryId, string Code, string Name, string Color, decimal Amount, int Count, decimal? MonthlyLimit, int? BudgetId);

    public record SpendingSummary(
        string Month,
        string Currency,
        decimal TotalIncome,
        decimal TotalExpense,
        IReadOnlyList<CategorySpending> Categories,
        IReadOnlyList<CategorizedTransaction> Transactions);
}
