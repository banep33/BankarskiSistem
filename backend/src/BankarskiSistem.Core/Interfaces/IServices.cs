using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Core.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> LoginAsync(string email, string password);
        Task<AuthResult> ChangePasswordAsync(int userId, Guid currentSessionId, string currentPassword, string newPassword);
        Task<bool> IsSessionValidAsync(int userId, int tokenVersion, string role, Guid sessionId);
        Task LogoutAsync(int userId, Guid sessionId);
    }

    public interface IUserService
    {
        Task<PagedResult<User>> GetPagedAsync(UserFilter filter, PageRequest page, Actor actor);
        Task<User> GetByIdAsync(int id, Actor actor);
        Task<User> CreateAsync(CreateUserCommand command, Actor actor);
        Task<User> UpdateAsync(int id, UpdateUserCommand command, Actor actor);
        Task<User> DeactivateAsync(int id, Actor actor);
        Task<User> ActivateAsync(int id, Actor actor);
        Task<User> UnlockAsync(int id, Actor actor);
        Task<User> UpdateKycAsync(int id, KycStatus status, RiskLevel riskRating, Actor actor);
        Task<User> PermanentDeleteAsync(int id, Actor actor);
        Task<User> GetProfileAsync(int userId);
        Task<User> UpdateProfileAsync(int userId, UpdateProfileCommand command);
        Task EnsureAdministratorExistsAsync(string email, string password, string firstName, string lastName);
    }

    public interface IAccountService
    {
        Task<PagedResult<Account>> GetPagedAsync(string? search, PageRequest page);
        Task<IReadOnlyList<Account>> GetByUserIdAsync(int userId, Actor actor);
        Task<Account> GetByIdAsync(int id, Actor actor);
        Task<Account> CreateAsync(int userId, string currency, Actor actor);
        Task<Account> ChangeStatusAsync(int accountId, AccountStatus status, Actor actor);
        Task DeleteAsync(int accountId, Actor actor);
    }

    public interface IPaymentService
    {
        Task<FinancialOperationResult> DepositAsync(DepositCommand command, Actor actor);
        Task<FinancialOperationResult> WithdrawAsync(WithdrawCommand command, Actor actor);
    }

    public interface IPaymentOrderService
    {
        Task<PaymentQuote> QuoteAsync(CreatePaymentOrderCommand command, Actor actor);
        Task<PaymentOrderResult> CreateAsync(CreatePaymentOrderCommand command, Actor actor);
        Task<PaymentOrder> CancelAsync(int orderId, Actor actor);
        Task<PaymentOrder> GetAsync(int orderId, Actor actor);
        Task<PagedResult<PaymentOrder>> GetPagedAsync(PaymentOrderFilter filter, PageRequest page, Actor actor);
        Task<int> ExecuteDueOrdersAsync(int batchSize, CancellationToken cancellationToken);
    }

    public interface IBeneficiaryService
    {
        Task<IReadOnlyList<Beneficiary>> GetAllAsync(int userId);
        Task<Beneficiary> CreateAsync(int userId, BeneficiaryCommand command);
        Task<Beneficiary> UpdateAsync(int userId, int beneficiaryId, BeneficiaryCommand command);
        Task DeleteAsync(int userId, int beneficiaryId);
    }

    public interface ITransactionService
    {
        Task<PagedResult<Transaction>> GetPagedAsync(TransactionFilter filter, PageRequest page, Actor actor);
        Task<CursorPage<Transaction>> GetCursorPageAsync(TransactionFilter filter, string? cursor, int limit, Actor actor);
        Task<Transaction> GetByIdAsync(int id, Actor actor);
        Task<Transaction> GetByReferenceAsync(string referenceNumber, Actor actor);
    }

    public interface INotificationService
    {
        Task<PagedResult<Notification>> GetPagedAsync(int userId, bool onlyUnread, PageRequest page);
        Task<int> CountUnreadAsync(int userId);
        Task MarkAsReadAsync(int userId, int notificationId);
        Task<int> MarkAllAsReadAsync(int userId);
    }

    public interface IStatisticsService
    {
        Task<CustomerDashboard> GetCustomerDashboardAsync(int userId);
        Task<AccountStatistics> GetAccountStatisticsAsync(int accountId, StatisticsPeriod period, Actor actor);
        Task<SystemOverview> GetSystemOverviewAsync();
    }

    public interface ILedgerQueryService
    {
        Task<IReadOnlyList<TrialBalanceLine>> GetTrialBalanceAsync();
        Task<IReadOnlyList<LedgerAccountBalance>> GetLedgerAccountBalancesAsync(bool systemOnly);
        Task<IReadOnlyList<ReconciliationLine>> GetReconciliationAsync(bool onlyDifferences);
    }

    public interface ILegalService
    {
        Task<IReadOnlyList<LegalDocument>> GetCurrentDocumentsAsync();
        Task<IReadOnlyList<LegalDocument>> GetPendingDocumentsAsync(int userId);
        Task<IReadOnlyList<UserConsent>> GetConsentsAsync(int userId);
        Task AcceptAsync(int userId, IReadOnlyCollection<int> documentIds);
        Task<LegalDocument> PublishAsync(PublishLegalDocumentCommand command, Actor actor);
    }

    public interface IPrivacyService
    {
        Task<PersonalDataExport> ExportAsync(int userId);
        Task<PagedResult<SecurityEvent>> GetSecurityEventsAsync(int userId, PageRequest page);
    }

    public interface IAuditLogService
    {
        Task LogAsync(AuditEntry entry);
        Task<PagedResult<AuditLog>> GetPagedAsync(AuditLogFilter filter, PageRequest page);
    }

    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verify(string password, string passwordHash);
    }

    public interface ITokenService
    {
        DateTime NextExpiry();
        string CreateToken(User user, Guid sessionId, DateTime expiresAt);
    }

    public interface IAccountNumberGenerator
    {
        string Generate();
    }

    public interface IRequestContext
    {
        string? IpAddress { get; }
        string? UserAgent { get; }
    }

    public interface IBusinessCalendar
    {
        DateTime StartOfTodayUtc();
        DateOnly Today();
        DateTime ToBusinessTime(DateTime utc);
        DateTime ToUtc(DateTime businessTime);
    }
}
