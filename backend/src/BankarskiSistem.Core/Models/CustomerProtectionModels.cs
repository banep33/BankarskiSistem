using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Models
{
    public record UpdatePreferencesCommand(
        bool NotifyIncomingPayments,
        bool NotifyOutgoingPayments,
        bool NotifyAccountChanges,
        bool NotifyComplaintUpdates,
        bool MarketingConsent,
        bool HideBalancesByDefault,
        decimal? ExtraConfirmationThreshold);

    public record SubmitComplaintCommand(
        ComplaintCategory Category,
        string Subject,
        string Description,
        int? RelatedTransactionId,
        int? RelatedAccountId);

    public record ComplaintFilter
    {
        public ComplaintStatus? Status { get; init; }
        public int? AssignedToUserId { get; init; }
        public bool OnlyOverdue { get; init; }
        public bool OnlyUnassigned { get; init; }
    }

    public record SubmitFeedbackCommand(SurveyType SurveyType, int Score, string? Comment, string Context);

    public record FaqArticleCommand(string Category, string Question, string Answer, int SortOrder, bool IsPublished);

    public record FeeQuote(decimal Fee, FeeScheduleEntry? Entry);

    public record PaymentQuote(
        decimal Amount,
        decimal Fee,
        decimal TotalDebit,
        string Currency,
        decimal BalanceBefore,
        decimal BalanceAfter,
        bool IsInternal,
        bool IsKnownRecipient,
        bool IsScheduled,
        string? FeeName,
        string? FeeDescription,
        bool RequiresStrongConfirmation,
        decimal RemainingDailyLimit,
        IReadOnlyList<string> Warnings);

    public record SurveyScore(SurveyType SurveyType, int Responses, decimal? Score, string Interpretation);

    public record FeedbackSummary(IReadOnlyList<SurveyScore> Scores, int TotalResponses);

    public record RetentionReport(int ReadNotificationsDeleted, int ExpiredSessionsDeleted);
}
