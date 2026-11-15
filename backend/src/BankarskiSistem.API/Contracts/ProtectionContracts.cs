using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.API.Contracts
{
    public record ReauthenticateRequest(string Password);

    public record UpdatePreferencesRequest(
        bool NotifyIncomingPayments,
        bool NotifyOutgoingPayments,
        bool NotifyAccountChanges,
        bool NotifyComplaintUpdates,
        bool MarketingConsent,
        bool HideBalancesByDefault,
        decimal? ExtraConfirmationThreshold);

    public record SubmitComplaintRequest(
        ComplaintCategory Category,
        string Subject,
        string Description,
        int? RelatedTransactionId,
        int? RelatedAccountId);

    public record ComplaintMessageRequest(string Message, bool IsInternalNote = false);

    public record AssignComplaintRequest(int? AssigneeUserId);

    public record ChangeComplaintStatusRequest(ComplaintStatus Status, string? Note);

    public record ExtendComplaintDeadlineRequest(string Reason);

    public record ComplaintQuery(ComplaintStatus? Status, int? AssignedToUserId, bool OnlyOverdue = false, bool OnlyUnassigned = false, int Page = 1, int PageSize = 20);

    public record SubmitFeedbackRequest(SurveyType SurveyType, int Score, string? Comment, string Context);

    public record FaqArticleRequest(string Category, string Question, string Answer, int SortOrder, bool IsPublished);

    public record SubmitPrivacyRequest(PrivacyRequestType RequestType, string Details);

    public record RespondPrivacyRequest(PrivacyRequestStatus Status, string Response);

    public record ReviewComplianceAlertRequest(ComplianceAlertStatus Status, string Note);

    public record UpdateKycRequest(KycStatus Status, RiskLevel RiskRating);

    public record SessionResponse(
        Guid Id,
        string? DeviceLabel,
        string? IpAddress,
        DateTime CreatedAt,
        DateTime LastSeenAt,
        DateTime ExpiresAt,
        bool IsCurrent);

    public record PreferencesResponse(
        bool NotifyIncomingPayments,
        bool NotifyOutgoingPayments,
        bool NotifyAccountChanges,
        bool NotifyComplaintUpdates,
        bool NotifySecurityEvents,
        bool MarketingConsent,
        bool HideBalancesByDefault,
        decimal? ExtraConfirmationThreshold);

    public record ConsentEventResponse(string ConsentType, bool Granted, string Source, DateTime CreatedAt);

    public record FeeResponse(
        string Code,
        string Name,
        string Description,
        FeeAppliesTo AppliesTo,
        FeeCalculationType CalculationType,
        decimal Amount,
        decimal? MinAmount,
        decimal? MaxAmount,
        string Currency,
        DateOnly ValidFrom,
        string HumanReadable);

    public record ComplaintSummaryResponse(
        int Id,
        string ReferenceNumber,
        ComplaintCategory Category,
        string Subject,
        ComplaintStatus Status,
        ComplaintPriority Priority,
        DateTime DueAt,
        bool IsOverdue,
        bool DeadlineExtended,
        string? CustomerName,
        string? AssignedTo,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public record ComplaintMessageResponse(int Id, bool IsFromStaff, bool IsInternalNote, string AuthorName, string Message, DateTime CreatedAt);

    public record ComplaintStatusChangeResponse(string? OldStatus, string NewStatus, string? Note, DateTime ChangedAt);

    public record ComplaintDetailResponse(
        ComplaintSummaryResponse Summary,
        string Description,
        int? RelatedTransactionId,
        int? RelatedAccountId,
        string? Resolution,
        DateTime? ResolvedAt,
        string? DeadlineExtensionReason,
        IReadOnlyList<ComplaintMessageResponse> Messages,
        IReadOnlyList<ComplaintStatusChangeResponse> History);

    public record FeedbackResponse(int Id, SurveyType SurveyType, int Score, string? Comment, string Context, DateTime CreatedAt);

    public record FaqResponse(int Id, string Category, string Question, string Answer, int SortOrder, bool IsPublished);

    public record DataAccessResponse(DateTime AccessedAt, string ViewerRole, string ViewerLabel, string Resource, string Purpose);

    public record PrivacyRequestResponse(
        int Id,
        string ReferenceNumber,
        PrivacyRequestType RequestType,
        string Details,
        PrivacyRequestStatus Status,
        DateTime DueAt,
        string? Response,
        DateTime? RespondedAt,
        DateTime CreatedAt,
        string? CustomerEmail);

    public record ComplianceAlertResponse(
        int Id,
        string RuleCode,
        RiskLevel Severity,
        int UserId,
        string? CustomerEmail,
        int? AccountId,
        int? TransactionId,
        string Details,
        ComplianceAlertStatus Status,
        string? ReviewNote,
        DateTime CreatedAt,
        DateTime? ReviewedAt,
        bool IsSimulation);

    public record SupportContactResponse(string Phone, string Email, string WorkingHours, string RegulatorInfo);

    public static class ProtectionMappings
    {
        public static SessionResponse ToResponse(this UserSession session, Guid currentSessionId) =>
            new(session.Id, session.DeviceLabel, session.IpAddress, session.CreatedAt, session.LastSeenAt, session.ExpiresAt,
                session.Id == currentSessionId);

        public static PreferencesResponse ToResponse(this UserPreferences preferences) =>
            new(preferences.NotifyIncomingPayments, preferences.NotifyOutgoingPayments, preferences.NotifyAccountChanges,
                preferences.NotifyComplaintUpdates, true, preferences.MarketingConsent, preferences.HideBalancesByDefault,
                preferences.ExtraConfirmationThreshold);

        public static FeeResponse ToResponse(this FeeScheduleEntry fee) =>
            new(fee.Code, fee.Name, fee.Description, fee.AppliesTo, fee.CalculationType, fee.Amount, fee.MinAmount, fee.MaxAmount,
                fee.Currency, fee.ValidFrom, Describe(fee));

        public static ComplaintSummaryResponse ToSummary(this Complaint complaint, DateTime utcNow) =>
            new(complaint.Id, complaint.ReferenceNumber, complaint.Category, complaint.Subject, complaint.Status, complaint.Priority,
                complaint.DueAt, complaint.IsOverdueAt(utcNow), complaint.DeadlineExtendedAt != null, complaint.User?.FullName,
                complaint.AssignedTo?.FullName, complaint.CreatedAt, complaint.UpdatedAt);

        public static ComplaintDetailResponse ToDetail(this Complaint complaint, DateTime utcNow) =>
            new(complaint.ToSummary(utcNow), complaint.Description, complaint.RelatedTransactionId, complaint.RelatedAccountId,
                complaint.Resolution, complaint.ResolvedAt, complaint.DeadlineExtensionReason,
                complaint.Messages.OrderBy(m => m.CreatedAt)
                    .Select(m => new ComplaintMessageResponse(m.Id, m.IsFromStaff, m.IsInternalNote,
                        m.IsFromStaff ? "Banka" : m.Author?.FullName ?? "Korisnik", m.Message, m.CreatedAt))
                    .ToList(),
                complaint.StatusHistory.OrderBy(h => h.ChangedAt)
                    .Select(h => new ComplaintStatusChangeResponse(h.OldStatus, h.NewStatus, h.Note, h.ChangedAt))
                    .ToList());

        public static FeedbackResponse ToResponse(this UserFeedback feedback) =>
            new(feedback.Id, feedback.SurveyType, feedback.Score, feedback.Comment, feedback.Context, feedback.CreatedAt);

        public static FaqResponse ToResponse(this FaqArticle article) =>
            new(article.Id, article.Category, article.Question, article.Answer, article.SortOrder, article.IsPublished);

        public static DataAccessResponse ToResponse(this DataAccessLog log) =>
            new(log.AccessedAt, log.Viewer?.Role.ToString() ?? "Zaposleni", $"Zaposleni br. {log.ViewerUserId}", log.Resource, log.Purpose);

        public static PrivacyRequestResponse ToResponse(this PrivacyRequest request) =>
            new(request.Id, request.ReferenceNumber, request.RequestType, request.Details, request.Status, request.DueAt,
                request.Response, request.RespondedAt, request.CreatedAt, request.User?.Email);

        public static ComplianceAlertResponse ToResponse(this ComplianceAlert alert) =>
            new(alert.Id, alert.RuleCode, alert.Severity, alert.UserId, alert.User?.Email, alert.AccountId, alert.TransactionId,
                alert.Details, alert.Status, alert.ReviewNote, alert.CreatedAt, alert.ReviewedAt, true);

        private static string Describe(FeeScheduleEntry fee)
        {
            if (fee.Amount == 0)
                return "Besplatno";
            if (fee.CalculationType == FeeCalculationType.Fiksna)
                return $"{fee.Amount:0.##} {fee.Currency}";

            var bounds = (fee.MinAmount, fee.MaxAmount) switch
            {
                ({ } min, { } max) => $", najmanje {min:0.##} a najviše {max:0.##} {fee.Currency}",
                ({ } min, null) => $", najmanje {min:0.##} {fee.Currency}",
                (null, { } max) => $", najviše {max:0.##} {fee.Currency}",
                _ => string.Empty
            };
            return $"{fee.Amount:0.##}% od iznosa{bounds}";
        }
    }
}
