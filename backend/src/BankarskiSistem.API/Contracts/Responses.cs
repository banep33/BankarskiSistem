using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.API.Contracts
{
    public record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages,
        bool HasPrevious,
        bool HasNext);

    public record CursorResponse<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

    public record LegalDocumentSummary(int Id, LegalDocumentType DocumentType, string Version, string Title, DateTime PublishedAt);

    public record LegalDocumentResponse(int Id, LegalDocumentType DocumentType, string Version, string Title, string Content, DateTime PublishedAt);

    public record ConsentResponse(int LegalDocumentId, LegalDocumentType DocumentType, string Version, string Title, DateTime AcceptedAt);

    public record LoginResponse(
        int Id,
        string Token,
        string Email,
        string FirstName,
        string LastName,
        UserRole Role,
        DateTime ExpiresAt,
        IReadOnlyList<LegalDocumentSummary> PendingDocuments);

    public record UserResponse(
        int Id,
        string FirstName,
        string LastName,
        string Email,
        UserRole Role,
        bool IsActive,
        bool IsLocked,
        DateTime? LockedUntil,
        int FailedLoginAttempts,
        KycStatus KycStatus,
        RiskLevel RiskRating,
        DateTime? KycVerifiedAt,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        bool TwoFactorEnabled);

    public record ProfileResponse(UserResponse User, IReadOnlyList<LegalDocumentSummary> PendingDocuments);

    public record AccountResponse(
        int Id,
        int UserId,
        string AccountNumber,
        decimal Balance,
        string Currency,
        AccountStatus Status,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string? OwnerFullName,
        string? OwnerEmail);

    public record TransactionResponse(
        int Id,
        string ReferenceNumber,
        int? FromAccountId,
        string? FromAccountNumber,
        int? ToAccountId,
        string? ToAccountNumber,
        decimal Amount,
        decimal FeeAmount,
        string Currency,
        TransactionType Type,
        TransactionStatus Status,
        string? Description,
        string? PerformedBy,
        DateTime CreatedAt,
        decimal? FromBalanceAfter,
        decimal? ToBalanceAfter,
        int? PaymentOrderId,
        string? CounterpartyName,
        string? CounterpartyAccountNumber,
        string? PaymentCode,
        string? PaymentReferenceModel,
        string? PaymentReferenceNumber);

    public record FinancialOperationResponse(
        string Message,
        string TransactionReference,
        int TransactionId,
        decimal Amount,
        string Currency,
        decimal BalanceAfter,
        DateTime ExecutedAt,
        bool IsReplay);

    public record PaymentOrderResponse(
        int Id,
        int FromAccountId,
        string? FromAccountNumber,
        string RecipientName,
        string RecipientAccountNumber,
        string? RecipientAddress,
        bool IsInternal,
        decimal Amount,
        decimal FeeAmount,
        decimal TotalDebit,
        string Currency,
        string PaymentCode,
        string Purpose,
        string? ReferenceModel,
        string? ReferenceNumber,
        PaymentOrderStatus Status,
        DateOnly? ScheduledFor,
        DateTime? ExecutedAt,
        string? RejectionReason,
        string? TransactionReference,
        decimal? BalanceAfter,
        DateTime CreatedAt,
        bool IsReplay);

    public record BeneficiaryResponse(
        int Id,
        string Nickname,
        string RecipientName,
        string AccountNumber,
        string? RecipientAddress,
        string? PaymentCode,
        string? Purpose,
        string? ReferenceModel,
        string? ReferenceNumber,
        DateTime? LastUsedAt);

    public record NotificationResponse(
        int Id,
        NotificationType Type,
        string Title,
        string Message,
        bool IsRead,
        int? TransactionId,
        DateTime CreatedAt);

    public record UnreadCountResponse(int Count);

    public record AuditLogResponse(
        int Id,
        int? UserId,
        string? UserEmail,
        string Action,
        string? EntityType,
        int? EntityId,
        string? IpAddress,
        string? OldValue,
        string? NewValue,
        bool IsSuccess,
        string? ErrorMessage,
        DateTime CreatedAt);

    public record CustomerDashboardResponse(
        IReadOnlyList<CurrencyAmount> BalancesByCurrency,
        IReadOnlyList<CashFlowSummary> MonthCashFlow,
        int ActiveAccounts,
        int TotalAccounts,
        int UnreadNotifications,
        IReadOnlyList<TransactionResponse> RecentTransactions);

    public record PaymentCodeResponse(string Code, string Description);

    public record ReferenceDataResponse(
        IReadOnlyList<PaymentCodeResponse> PaymentCodes,
        IReadOnlyList<string> ReferenceModels,
        IReadOnlyList<string> Currencies,
        string BankCode);

    public record ApiProblem(
        string Type,
        string Title,
        int Status,
        string Detail,
        string Instance,
        string TraceId,
        IReadOnlyList<string> Errors);
}
