using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Core.Interfaces
{
    public interface IUnitOfWork
    {
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    }

    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string normalizedEmail);
        Task<bool> EmailExistsAsync(string normalizedEmail, int? excludeUserId = null);
        Task<bool> AnyAdministratorAsync();
        Task<bool> HasFinancialHistoryAsync(int userId);
        Task<PagedResult<User>> GetPagedAsync(UserFilter filter, PageRequest page);
        Task AddAsync(User user);
        void Remove(User user);
    }

    public interface IAccountRepository
    {
        Task<Account?> GetByIdAsync(int id);
        Task<Account?> FindByNumberAsync(string accountNumber);
        Task<IReadOnlyList<Account>> GetByUserIdAsync(int userId);
        Task<PagedResult<Account>> GetPagedAsync(string? search, PageRequest page);
        Task<IReadOnlyList<Account>> LockForUpdateAsync(IReadOnlyCollection<int> accountIds);
        Task<bool> AccountNumberExistsAsync(string accountNumber);
        Task<bool> HasTransactionsAsync(int accountId);
        Task AddAsync(Account account);
        void Remove(Account account);
    }

    public interface ITransactionRepository
    {
        Task<Transaction?> GetByIdAsync(int id);
        Task<Transaction?> GetByReferenceAsync(string referenceNumber);
        Task<Transaction?> GetByIdempotencyKeyAsync(int userId, string idempotencyKey);
        Task<PagedResult<Transaction>> GetPagedAsync(TransactionFilter filter, PageRequest page);
        Task<IReadOnlyList<Transaction>> GetRecentForUserAsync(int userId, int take);
        Task<CursorPage<Transaction>> GetCursorPageAsync(TransactionFilter filter, string? cursor, int limit);
        Task<(decimal Total, int Count)> GetOutgoingSinceAsync(int accountId, DateTime sinceUtc);
        Task AddAsync(Transaction transaction);
    }

    public interface IPaymentOrderRepository
    {
        Task<PaymentOrder?> GetForUserAsync(int userId, int orderId);
        Task<PaymentOrder?> GetByIdempotencyKeyAsync(int userId, string idempotencyKey);
        Task<PagedResult<PaymentOrder>> GetPagedForUserAsync(int userId, PaymentOrderFilter filter, PageRequest page);
        Task<IReadOnlyList<int>> GetDueOrderIdsAsync(DateOnly businessDate, int take);
        Task<PaymentOrder?> TryLockDueOrderAsync(int orderId);
        Task AddAsync(PaymentOrder order);
    }

    public interface IBeneficiaryRepository
    {
        Task<IReadOnlyList<Beneficiary>> GetForUserAsync(int userId);
        Task<Beneficiary?> GetAsync(int userId, int beneficiaryId);
        Task<Beneficiary?> FindByNicknameAsync(int userId, string nickname);
        Task AddAsync(Beneficiary beneficiary);
        void Remove(Beneficiary beneficiary);
    }

    public interface ILegalRepository
    {
        Task<IReadOnlyList<LegalDocument>> GetCurrentDocumentsAsync();
        Task<IReadOnlyList<LegalDocument>> GetPendingForUserAsync(int userId);
        Task<IReadOnlyList<UserConsent>> GetConsentsForUserAsync(int userId);
        Task<bool> VersionExistsAsync(LegalDocumentType type, string version);
        Task AddConsentAsync(UserConsent consent);
        Task AddDocumentAsync(LegalDocument document);
    }

    public interface INotificationRepository
    {
        Task<PagedResult<Notification>> GetPagedAsync(int userId, bool onlyUnread, PageRequest page);
        Task<int> CountUnreadAsync(int userId);
        Task<Notification?> GetAsync(int userId, int notificationId);
        Task<int> MarkAllAsReadAsync(int userId);
        Task AddRangeAsync(IEnumerable<Notification> notifications);
    }

    public interface ILedgerRepository
    {
        Task<IReadOnlyDictionary<int, int>> GetCustomerLedgerAccountIdsAsync(IReadOnlyCollection<int> accountIds);
        Task<int> GetSystemLedgerAccountIdAsync(LedgerAccountType type, string currency);
        Task EnsureSystemAccountsAsync(string currency);
        Task AddLedgerAccountAsync(LedgerAccount ledgerAccount);
        Task AddJournalEntryAsync(JournalEntry entry);
    }

    public interface IAuditLogRepository
    {
        Task AddAsync(AuditLog auditLog);
        Task<PagedResult<AuditLog>> GetPagedAsync(AuditLogFilter filter, PageRequest page);
    }
}
