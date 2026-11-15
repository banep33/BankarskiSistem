using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Core.Interfaces
{
    public interface IAuthService
    {
        Task<LoginOutcome> LoginAsync(string email, string password);
        Task<AuthResult> CompleteTwoFactorAsync(string challengeToken, string code);
        Task<AuthResult> RefreshAsync(string refreshToken);
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
        DateTime NextSessionExpiry();
        string CreateToken(User user, Guid sessionId, DateTime expiresAt);
        string CreateTwoFactorChallenge(User user, DateTime expiresAt);
        (int UserId, int TokenVersion)? ReadTwoFactorChallenge(string token);
    }

    public interface IBudgetService
    {
        Task<IReadOnlyList<SpendingCategory>> GetCategoriesAsync();
        Task<SpendingSummary> GetSpendingAsync(Actor actor, string? month);
        Task AssignCategoryAsync(Actor actor, int transactionId, int categoryId, bool rememberForCounterparty);
        Task<IReadOnlyList<Budget>> GetBudgetsAsync(Actor actor);
        Task<Budget> SetBudgetAsync(Actor actor, int categoryId, decimal monthlyLimit);
        Task DeleteBudgetAsync(Actor actor, int budgetId);
        Task EvaluateAlertsAsync(int userId);
        Task TryEvaluateAlertsAsync(int userId);
    }

    public interface IStatementService
    {
        Task<AccountStatement> GetAsync(int accountId, DateOnly from, DateOnly to, Actor actor);
    }

    public interface IReversalService
    {
        Task<TransactionReversal> RequestAsync(int transactionId, string reason, Actor actor);
        Task<PagedResult<TransactionReversal>> GetPagedAsync(ReversalStatus? status, PageRequest page, Actor actor);
        Task<TransactionReversal> ApproveAsync(int requestId, string? note, Actor actor);
        Task<TransactionReversal> RejectAsync(int requestId, string note, Actor actor);
    }

    public interface ICurrencyExchangeService
    {
        Task<IReadOnlyList<ExchangeRate>> GetCurrentRatesAsync();
        Task<IReadOnlyList<ExchangeRate>> GetHistoryAsync(string currency, int days);
        Task<IReadOnlyList<ExchangeRate>> PublishRatesAsync(IReadOnlyList<ExchangeRateInput> rates, Actor actor);
        Task<ExchangeQuote> QuoteAsync(CurrencyExchangeCommand command, Actor actor);
        Task<CurrencyExchangeResult> ExecuteAsync(CurrencyExchangeCommand command, Actor actor);
        Task<PagedResult<CurrencyExchange>> GetMineAsync(Actor actor, PageRequest page);
    }

    public interface IStandingOrderService
    {
        Task<IReadOnlyList<StandingOrder>> GetMineAsync(Actor actor);
        Task<IReadOnlyList<StandingOrder>> GetForUserAsync(int userId, Actor actor);
        Task<StandingOrder> GetAsync(int id, Actor actor);
        Task<StandingOrder> CreateAsync(CreateStandingOrderCommand command, Actor actor);
        Task<StandingOrder> UpdateAsync(int id, UpdateStandingOrderCommand command, Actor actor);
        Task<StandingOrder> PauseAsync(int id, Actor actor);
        Task<StandingOrder> ResumeAsync(int id, Actor actor);
        Task<StandingOrder> CancelAsync(int id, Actor actor);
        Task<int> ExecuteDueAsync(int batchSize, CancellationToken cancellationToken);
    }

    public interface ISecretProtector
    {
        string Protect(string plaintext);
        string Unprotect(string protectedValue);
    }

    public interface ITwoFactorService
    {
        Task<TwoFactorStatus> GetStatusAsync(int userId);
        Task<TwoFactorSetup> BeginSetupAsync(int userId);
        Task<IReadOnlyList<string>> EnableAsync(int userId, string code);
        Task DisableAsync(int userId, string code);
        Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(int userId, string code);
        Task<bool> VerifyLoginCodeAsync(User user, string code);
        Task ResetByAdministratorAsync(int userId, Actor actor);
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
