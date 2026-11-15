using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.API.Contracts
{
    public record LoginRequest(string Email, string Password);

    public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

    public record CreateUserRequest(
        string FirstName,
        string LastName,
        string Email,
        string Password,
        string ConfirmPassword,
        UserRole Role);

    public record UpdateUserRequest(string FirstName, string LastName, string Email, UserRole Role);

    public record UpdateProfileRequest(string FirstName, string LastName);

    public record CreateAccountRequest(int UserId, string Currency);

    public record ChangeAccountStatusRequest(AccountStatus Status);

    public record MoneyRequest(decimal Amount, string? Description);

    public record CreatePaymentRequest(
        int FromAccountId,
        string RecipientName,
        string RecipientAccountNumber,
        string? RecipientAddress,
        decimal Amount,
        string PaymentCode,
        string Purpose,
        string? ReferenceModel,
        string? ReferenceNumber,
        DateOnly? ScheduledFor,
        string? SaveAsBeneficiary);

    public record BeneficiaryRequest(
        string Nickname,
        string RecipientName,
        string AccountNumber,
        string? RecipientAddress,
        string? PaymentCode,
        string? Purpose,
        string? ReferenceModel,
        string? ReferenceNumber);

    public record AcceptDocumentsRequest(IReadOnlyList<int> DocumentIds);

    public record PublishLegalDocumentRequest(LegalDocumentType DocumentType, string Version, string Title, string Content);

    public record TransactionCursorQuery(
        int? AccountId,
        TransactionType? Type,
        TransactionStatus? Status,
        DateOnly? From,
        DateOnly? To,
        string? Search,
        string? Cursor,
        int Limit = 20);

    public record TransactionQuery(
        int? AccountId,
        TransactionType? Type,
        TransactionStatus? Status,
        DateOnly? From,
        DateOnly? To,
        string? Search,
        int Page = 1,
        int PageSize = 20);

    public record PaymentOrderQuery(PaymentOrderStatus? Status, int? AccountId, int Page = 1, int PageSize = 20);

    public record UserQuery(UserRole? Role, bool? IsActive, string? Search, int Page = 1, int PageSize = 20);

    public record AccountQuery(string? Search, int Page = 1, int PageSize = 20);

    public record AuditLogQuery(
        int? UserId,
        bool? OnlyFailures,
        string? Search,
        DateOnly? From,
        DateOnly? To,
        int Page = 1,
        int PageSize = 50);
}
