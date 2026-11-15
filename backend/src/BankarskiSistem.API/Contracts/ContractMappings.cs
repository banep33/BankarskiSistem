using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.API.Contracts
{
    public static class ContractMappings
    {
        public static PagedResponse<TResponse> ToResponse<TSource, TResponse>(
            this PagedResult<TSource> result, Func<TSource, TResponse> map) =>
            new(
                result.Items.Select(map).ToList(),
                result.TotalCount,
                result.Page,
                result.PageSize,
                result.TotalPages,
                result.HasPrevious,
                result.HasNext);

        public static CursorResponse<TResponse> ToResponse<TSource, TResponse>(
            this CursorPage<TSource> page, Func<TSource, TResponse> map) =>
            new(page.Items.Select(map).ToList(), page.NextCursor, page.HasMore);

        public static LegalDocumentSummary ToSummary(this LegalDocument document) =>
            new(document.Id, document.DocumentType, document.Version, document.Title, document.PublishedAt);

        public static LegalDocumentResponse ToResponse(this LegalDocument document) =>
            new(document.Id, document.DocumentType, document.Version, document.Title, document.Content, document.PublishedAt);

        public static ConsentResponse ToResponse(this UserConsent consent) =>
            new(
                consent.LegalDocumentId,
                consent.LegalDocument!.DocumentType,
                consent.LegalDocument.Version,
                consent.LegalDocument.Title,
                consent.AcceptedAt);

        public static LoginResponse ToResponse(this AuthResult result, IReadOnlyList<LegalDocument> pendingDocuments) =>
            new(
                result.User.Id,
                result.Token,
                result.User.Email,
                result.User.FirstName,
                result.User.LastName,
                result.User.Role,
                result.ExpiresAt,
                pendingDocuments.Select(d => d.ToSummary()).ToList());

        public static UserResponse ToResponse(this User user) =>
            new(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Role,
                user.IsActive,
                user.IsLocked,
                user.LockedUntil,
                user.FailedLoginAttempts,
                user.KycStatus,
                user.RiskRating,
                user.KycVerifiedAt,
                user.CreatedAt,
                user.UpdatedAt,
                user.TwoFactorEnabled);

        public static AccountResponse ToResponse(this Account account) =>
            new(
                account.Id,
                account.UserId,
                account.AccountNumber,
                account.Balance,
                account.Currency,
                account.Status,
                account.CreatedAt,
                account.UpdatedAt,
                account.User?.FullName,
                account.User?.Email,
                account.Kind);

        public static TransactionResponse ToResponse(this Transaction transaction) =>
            new(
                transaction.Id,
                transaction.ReferenceNumber,
                transaction.FromAccountId,
                transaction.FromAccount?.AccountNumber,
                transaction.ToAccountId,
                transaction.ToAccount?.AccountNumber,
                transaction.Amount,
                transaction.FeeAmount,
                transaction.Currency,
                transaction.Type,
                transaction.Status,
                transaction.Description,
                transaction.PerformedBy?.FullName,
                transaction.CreatedAt,
                transaction.FromBalanceAfter,
                transaction.ToBalanceAfter,
                transaction.PaymentOrder?.Id,
                transaction.PaymentOrder?.RecipientName,
                transaction.PaymentOrder?.RecipientAccountNumber ?? transaction.ToAccount?.AccountNumber,
                transaction.PaymentOrder?.PaymentCode,
                transaction.PaymentOrder?.ReferenceModel,
                transaction.PaymentOrder?.ReferenceNumber,
                transaction.ReversalOfTransactionId,
                transaction.ReversedAt);

        public static FinancialOperationResponse ToResponse(this FinancialOperationResult result, string message) =>
            new(
                message,
                result.Transaction.ReferenceNumber,
                result.Transaction.Id,
                result.Transaction.Amount,
                result.Transaction.Currency,
                result.BalanceAfter,
                result.Transaction.CreatedAt,
                result.IsReplay);

        public static PaymentOrderResponse ToResponse(this PaymentOrder order, bool isReplay = false) =>
            new(
                order.Id,
                order.FromAccountId,
                order.FromAccount?.AccountNumber,
                order.RecipientName,
                order.RecipientAccountNumber,
                order.RecipientAddress,
                order.IsInternal,
                order.Amount,
                order.FeeAmount,
                order.Amount + order.FeeAmount,
                order.Currency,
                order.PaymentCode,
                order.Purpose,
                order.ReferenceModel,
                order.ReferenceNumber,
                order.Status,
                order.ScheduledFor,
                order.ExecutedAt,
                order.RejectionReason,
                order.Transaction?.ReferenceNumber,
                order.Transaction?.FromBalanceAfter,
                order.CreatedAt,
                isReplay);

        public static BeneficiaryResponse ToResponse(this Beneficiary beneficiary) =>
            new(
                beneficiary.Id,
                beneficiary.Nickname,
                beneficiary.RecipientName,
                beneficiary.AccountNumber,
                beneficiary.RecipientAddress,
                beneficiary.PaymentCode,
                beneficiary.Purpose,
                beneficiary.ReferenceModel,
                beneficiary.ReferenceNumber,
                beneficiary.LastUsedAt);

        public static NotificationResponse ToResponse(this Notification notification) =>
            new(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.IsRead,
                notification.TransactionId,
                notification.CreatedAt);

        public static AuditLogResponse ToResponse(this AuditLog log) =>
            new(
                log.Id,
                log.UserId,
                log.User?.Email,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.IpAddress,
                log.OldValue,
                log.NewValue,
                log.IsSuccess,
                log.ErrorMessage,
                log.CreatedAt);

        public static CustomerDashboardResponse ToResponse(this CustomerDashboard dashboard) =>
            new(
                dashboard.BalancesByCurrency,
                dashboard.MonthCashFlow,
                dashboard.ActiveAccounts,
                dashboard.TotalAccounts,
                dashboard.UnreadNotifications,
                dashboard.RecentTransactions.Select(t => t.ToResponse()).ToList());
    }
}
