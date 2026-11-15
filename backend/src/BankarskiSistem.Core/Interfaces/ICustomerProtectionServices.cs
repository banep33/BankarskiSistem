using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Core.Interfaces
{
    public interface ISessionService
    {
        Task<UserSession> StartAsync(int userId, DateTime expiresAt);
        Task<bool> ValidateAndTouchAsync(Guid sessionId, int userId);
        Task<IReadOnlyList<UserSession>> GetActiveAsync(int userId);
        Task RevokeAsync(int userId, Guid sessionId, string reason);
        Task<int> RevokeOthersAsync(int userId, Guid currentSessionId, string reason);
        Task<int> RevokeAllAsync(int userId, string reason);
        Task ReauthenticateAsync(int userId, Guid sessionId, string password);
        Task<bool> IsRecentlyReauthenticatedAsync(Guid sessionId);
    }

    public interface ISessionValidationCache
    {
        bool IsKnownValid(Guid sessionId, int userId, int tokenVersion, string role);
        void Remember(Guid sessionId, int userId, int tokenVersion, string role, DateTime sessionExpiresAt);
        void InvalidateSession(Guid sessionId);
        void InvalidateUser(int userId);
    }

    public interface IPreferencesService
    {
        Task<UserPreferences> GetAsync(int userId);
        Task<UserPreferences> UpdateAsync(int userId, UpdatePreferencesCommand command);
        Task<IReadOnlyList<ConsentEvent>> GetConsentHistoryAsync(int userId);
    }

    public interface IFeeService
    {
        Task<IReadOnlyList<FeeScheduleEntry>> GetCurrentScheduleAsync();
        Task<FeeQuote> QuoteAsync(FeeAppliesTo appliesTo, decimal principal, string currency);
    }

    public interface IComplaintService
    {
        Task<Complaint> SubmitAsync(int userId, SubmitComplaintCommand command);
        Task<PagedResult<Complaint>> GetMineAsync(int userId, PageRequest page);
        Task<Complaint> GetMineAsync(int userId, int complaintId);
        Task<Complaint> AddCustomerMessageAsync(int userId, int complaintId, string message);
        Task<PagedResult<Complaint>> GetPagedAsync(ComplaintFilter filter, PageRequest page, Actor actor);
        Task<Complaint> GetForStaffAsync(int complaintId, Actor actor);
        Task<Complaint> AssignAsync(int complaintId, int? assigneeUserId, Actor actor);
        Task<Complaint> ChangeStatusAsync(int complaintId, ComplaintStatus status, string? note, Actor actor);
        Task<Complaint> ExtendDeadlineAsync(int complaintId, string reason, Actor actor);
        Task<Complaint> AddStaffMessageAsync(int complaintId, string message, bool isInternalNote, Actor actor);
    }

    public interface IFeedbackService
    {
        Task<UserFeedback> SubmitAsync(int userId, SubmitFeedbackCommand command);
        Task<FeedbackSummary> GetSummaryAsync(DateTime? sinceUtc);
        Task<PagedResult<UserFeedback>> GetRecentAsync(SurveyType? type, PageRequest page);
    }

    public interface IFaqService
    {
        Task<IReadOnlyList<FaqArticle>> GetPublishedAsync();
        Task<IReadOnlyList<FaqArticle>> GetAllAsync();
        Task<FaqArticle> SaveAsync(int? articleId, FaqArticleCommand command, Actor actor);
        Task DeleteAsync(int articleId, Actor actor);
    }

    public interface IDataAccessLogger
    {
        Task LogAsync(Actor viewer, int subjectUserId, string resource, int? resourceId, string purpose);
        Task<PagedResult<DataAccessLog>> GetForSubjectAsync(int subjectUserId, PageRequest page);
    }

    public interface IPrivacyRequestService
    {
        Task<PrivacyRequest> SubmitAsync(int userId, PrivacyRequestType type, string details);
        Task<IReadOnlyList<PrivacyRequest>> GetMineAsync(int userId);
        Task<PagedResult<PrivacyRequest>> GetPagedAsync(PrivacyRequestStatus? status, PageRequest page, Actor actor);
        Task<PrivacyRequest> RespondAsync(int requestId, PrivacyRequestStatus status, string response, Actor actor);
    }

    public interface IComplianceMonitor
    {
        Task EvaluateAsync(Transaction transaction, Account account, string? recipientAccountNumber);
    }

    public interface IComplianceService
    {
        Task<PagedResult<ComplianceAlert>> GetPagedAsync(ComplianceAlertStatus? status, RiskLevel? severity, PageRequest page, Actor actor);
        Task<ComplianceAlert> ReviewAsync(int alertId, ComplianceAlertStatus status, string note, Actor actor);
    }

    public interface IPasswordResetService
    {
        Task<PasswordResetRequest?> SubmitAsync(string email, string newPassword);
        Task<PagedResult<PasswordResetRequest>> GetPagedAsync(PasswordResetStatus? status, PageRequest page, Actor actor);
        Task<int> CountPendingAsync(Actor actor);
        Task<PasswordResetRequest> ApproveAsync(int requestId, string? note, Actor actor);
        Task<PasswordResetRequest> RejectAsync(int requestId, string note, Actor actor);
    }

    public interface INotificationPublisher
    {
        Task PublishAsync(params Notification[] notifications);
    }

    public interface IDataRetentionService
    {
        Task<RetentionReport> PurgeExpiredAsync(CancellationToken cancellationToken);
    }
}
