using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Entities
{
    public class UserSession
    {
        public Guid Id { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? ReauthenticatedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevokedReason { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? DeviceLabel { get; set; }

        public bool IsActiveAt(DateTime utcNow) => RevokedAt == null && ExpiresAt > utcNow;

        public void Revoke(string reason, DateTime utcNow)
        {
            if (RevokedAt != null)
                return;
            RevokedAt = utcNow;
            RevokedReason = reason.Length > 60 ? reason[..60] : reason;
        }
    }

    public class UserPreferences
    {
        public int UserId { get; set; }
        public bool NotifyIncomingPayments { get; set; } = true;
        public bool NotifyOutgoingPayments { get; set; } = true;
        public bool NotifyAccountChanges { get; set; } = true;
        public bool NotifyComplaintUpdates { get; set; } = true;
        public bool MarketingConsent { get; set; }
        public bool HideBalancesByDefault { get; set; }
        public decimal? ExtraConfirmationThreshold { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool Allows(NotificationType type) => type switch
        {
            NotificationType.PrimljenTransfer or NotificationType.Uplata => NotifyIncomingPayments,
            NotificationType.PoslatTransfer or NotificationType.Isplata => NotifyOutgoingPayments,
            NotificationType.StatusRacuna or NotificationType.NoviRacun => NotifyAccountChanges,
            NotificationType.Prigovor => NotifyComplaintUpdates,
            _ => true
        };
    }

    public class ConsentEvent
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public string ConsentType { get; set; } = string.Empty;
        public bool Granted { get; set; }
        public string Source { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class FeeScheduleEntry
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public FeeAppliesTo AppliesTo { get; set; }
        public FeeCalculationType CalculationType { get; set; }
        public decimal Amount { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public string Currency { get; set; } = "RSD";
        public DateOnly ValidFrom { get; set; }
        public DateOnly? ValidTo { get; set; }

        public decimal CalculateFor(decimal principal)
        {
            var fee = CalculationType == FeeCalculationType.Fiksna
                ? Amount
                : Math.Round(principal * Amount / 100m, 2, MidpointRounding.AwayFromZero);

            if (MinAmount.HasValue && fee < MinAmount.Value)
                fee = MinAmount.Value;
            if (MaxAmount.HasValue && fee > MaxAmount.Value)
                fee = MaxAmount.Value;
            return Math.Round(fee, 2, MidpointRounding.AwayFromZero);
        }
    }

    public class Complaint
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public int UserId { get; set; }
        public ComplaintCategory Category { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? RelatedTransactionId { get; set; }
        public int? RelatedAccountId { get; set; }
        public ComplaintStatus Status { get; set; } = ComplaintStatus.Podnet;
        public ComplaintPriority Priority { get; set; } = ComplaintPriority.Normalan;
        public int? AssignedToUserId { get; set; }
        public DateTime DueAt { get; set; }
        public DateTime? DeadlineExtendedAt { get; set; }
        public string? DeadlineExtensionReason { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? Resolution { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public User? AssignedTo { get; set; }
        public ICollection<ComplaintMessage> Messages { get; set; } = new List<ComplaintMessage>();
        public ICollection<ComplaintStatusChange> StatusHistory { get; set; } = new List<ComplaintStatusChange>();

        public bool IsOpen => Status is ComplaintStatus.Podnet or ComplaintStatus.UObradi or ComplaintStatus.CekaKorisnika;
        public bool IsOverdueAt(DateTime utcNow) => IsOpen && DueAt < utcNow;
    }

    public class ComplaintMessage
    {
        public int Id { get; set; }
        public int ComplaintId { get; set; }
        public int AuthorUserId { get; set; }
        public bool IsFromStaff { get; set; }
        public bool IsInternalNote { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? Author { get; set; }
    }

    public class ComplaintStatusChange
    {
        public int Id { get; set; }
        public int ComplaintId { get; set; }
        public string? OldStatus { get; set; }
        public string NewStatus { get; set; } = string.Empty;
        public int ChangedByUserId { get; set; }
        public string? Note { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserFeedback
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public SurveyType SurveyType { get; set; }
        public int Score { get; set; }
        public string? Comment { get; set; }
        public string Context { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class FaqArticle
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsPublished { get; set; } = true;
        public int? UpdatedByUserId { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DataAccessLog
    {
        public long Id { get; set; }
        public int ViewerUserId { get; set; }
        public int SubjectUserId { get; set; }
        public string Resource { get; set; } = string.Empty;
        public int? ResourceId { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

        public User? Viewer { get; set; }
    }

    public class PrivacyRequest
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public int UserId { get; set; }
        public PrivacyRequestType RequestType { get; set; }
        public string Details { get; set; } = string.Empty;
        public PrivacyRequestStatus Status { get; set; } = PrivacyRequestStatus.Podnet;
        public DateTime DueAt { get; set; }
        public int? HandledByUserId { get; set; }
        public string? Response { get; set; }
        public DateTime? RespondedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }

    public class PasswordResetRequest
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string? NewPasswordHash { get; set; }
        public PasswordResetStatus Status { get; set; } = PasswordResetStatus.Podnet;
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int? ReviewedByUserId { get; set; }
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public User? ReviewedBy { get; set; }
    }

    public class ComplianceAlert
    {
        public int Id { get; set; }
        public string RuleCode { get; set; } = string.Empty;
        public RiskLevel Severity { get; set; }
        public int UserId { get; set; }
        public int? AccountId { get; set; }
        public int? TransactionId { get; set; }
        public string Details { get; set; } = string.Empty;
        public ComplianceAlertStatus Status { get; set; } = ComplianceAlertStatus.Otvoren;
        public int? ReviewedByUserId { get; set; }
        public string? ReviewNote { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        public User? User { get; set; }
    }
}
