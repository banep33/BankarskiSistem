using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Models
{
    public record AuthResult(User User, string Token, DateTime ExpiresAt, Guid SessionId);

    public record FinancialOperationResult(
        Transaction Transaction,
        decimal BalanceAfter,
        string? CounterpartyAccountNumber,
        bool IsReplay);

    public record CurrencyAmount(string Currency, decimal Amount);

    public record CashFlowSummary(string Currency, decimal Income, decimal Expense);

    public record CashFlowBucket(DateTime PeriodStart, decimal Income, decimal Expense, int Count);

    public record CustomerDashboard(
        IReadOnlyList<CurrencyAmount> BalancesByCurrency,
        IReadOnlyList<CashFlowSummary> MonthCashFlow,
        int ActiveAccounts,
        int TotalAccounts,
        int UnreadNotifications,
        IReadOnlyList<Transaction> RecentTransactions);

    public record AccountStatistics(
        int AccountId,
        string Currency,
        StatisticsPeriod Period,
        decimal TotalIncome,
        decimal TotalExpense,
        IReadOnlyList<CashFlowBucket> Buckets);

    public record SystemOverview(
        int TotalUsers,
        int Customers,
        int Bankers,
        int Administrators,
        int ActiveUsers,
        int LockedUsers,
        int TotalAccounts,
        int ActiveAccounts,
        int BlockedAccounts,
        IReadOnlyList<CurrencyAmount> DepositsByCurrency,
        int TransactionsToday,
        IReadOnlyList<CurrencyAmount> VolumeTodayByCurrency,
        int FailedLoginsLast24Hours,
        IReadOnlyList<ActivityBucket> ActivityLast7Days);

    public record ActivityBucket(DateTime Day, int Deposits, int Withdrawals, int Transfers);

    public record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

    public record PaymentOrderResult(PaymentOrder Order, bool IsReplay);

    public record LedgerAccountBalance(int LedgerAccountId, string Code, string Name, LedgerAccountType Type, string Currency, decimal Balance);

    public record TrialBalanceLine(string Currency, decimal Total, int PostingCount);

    public record ReconciliationLine(int AccountId, string AccountNumber, string Currency, decimal StoredBalance, decimal LedgerBalance, decimal Difference);

    public record SecurityEvent(DateTime OccurredAt, string Action, bool IsSuccess, string? IpAddress);

    public record PersonalDataExport(
        DateTime GeneratedAt,
        object Profile,
        IReadOnlyList<object> Accounts,
        IReadOnlyList<object> Transactions,
        IReadOnlyList<object> PaymentOrders,
        IReadOnlyList<object> Beneficiaries,
        IReadOnlyList<object> Consents,
        IReadOnlyList<object> Notifications,
        IReadOnlyList<object> SecurityEvents);
}
