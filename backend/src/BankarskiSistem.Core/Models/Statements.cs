using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Models
{
    public record StatementLine(
        DateTime Date,
        string Reference,
        TransactionType? Type,
        string Description,
        string Counterparty,
        decimal Debit,
        decimal Credit,
        decimal BalanceAfter,
        int? TransactionId);

    public record AccountStatement(
        int AccountId,
        string AccountNumber,
        string Currency,
        int OwnerUserId,
        string OwnerName,
        DateOnly From,
        DateOnly To,
        decimal OpeningBalance,
        decimal TotalDebits,
        decimal TotalCredits,
        decimal ClosingBalance,
        DateTime GeneratedAt,
        IReadOnlyList<StatementLine> Lines);
}
