using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Services
{
    public partial class FeedbackService : IFeedbackService
    {
        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;

        public FeedbackService(ApplicationDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<UserFeedback> SubmitAsync(int userId, SubmitFeedbackCommand command)
        {
            var (min, max) = ScoreRange(command.SurveyType);
            if (command.Score < min || command.Score > max)
                throw new BusinessRuleException($"Ocena za {command.SurveyType} mora biti između {min} i {max}.");

            if (!ContextPattern().IsMatch(command.Context))
                throw new BusinessRuleException("Neispravan kontekst ocene.");

            if (await _context.UserFeedback.AnyAsync(f => f.UserId == userId && f.SurveyType == command.SurveyType && f.Context == command.Context))
                throw new ConflictException("Već ste ostavili ocenu za ovu stavku. Hvala!");

            var feedback = new UserFeedback
            {
                UserId = userId,
                SurveyType = command.SurveyType,
                Score = command.Score,
                Comment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim(),
                Context = command.Context,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            await _context.UserFeedback.AddAsync(feedback);
            await _context.SaveChangesAsync();
            return feedback;
        }

        public async Task<FeedbackSummary> GetSummaryAsync(DateTime? sinceUtc)
        {
            var query = _context.UserFeedback.AsNoTracking();
            if (sinceUtc.HasValue)
                query = query.Where(f => f.CreatedAt >= sinceUtc.Value);

            var scores = await query.Select(f => new { f.SurveyType, f.Score }).ToListAsync();

            var summary = Enum.GetValues<SurveyType>().Select(type =>
            {
                var values = scores.Where(s => s.SurveyType == type).Select(s => s.Score).ToList();
                if (values.Count == 0)
                    return new SurveyScore(type, 0, null, "Nema odgovora");

                return type switch
                {
                    SurveyType.CSAT => CsatScore(values),
                    SurveyType.NPS => NpsScore(values),
                    _ => CesScore(values)
                };
            }).ToList();

            return new FeedbackSummary(summary, scores.Count);
        }

        public Task<PagedResult<UserFeedback>> GetRecentAsync(SurveyType? type, PageRequest page)
        {
            var query = _context.UserFeedback.AsNoTracking();
            if (type.HasValue)
                query = query.Where(f => f.SurveyType == type.Value);
            return query.OrderByDescending(f => f.CreatedAt).ToPagedResultAsync(page);
        }

        public static (int Min, int Max) ScoreRange(SurveyType type) => type switch
        {
            SurveyType.CSAT => (1, 5),
            SurveyType.NPS => (0, 10),
            _ => (1, 7)
        };

        private static SurveyScore CsatScore(List<int> values)
        {
            var satisfied = Math.Round(values.Count(v => v >= 4) * 100m / values.Count, 1);
            return new SurveyScore(SurveyType.CSAT, values.Count, satisfied, $"{satisfied}% zadovoljnih (ocena 4 ili 5)");
        }

        private static SurveyScore NpsScore(List<int> values)
        {
            var promoters = values.Count(v => v >= 9) * 100m / values.Count;
            var detractors = values.Count(v => v <= 6) * 100m / values.Count;
            var nps = Math.Round(promoters - detractors, 1);
            return new SurveyScore(SurveyType.NPS, values.Count, nps, $"NPS {nps} (promoteri {promoters:0.#}%, kritičari {detractors:0.#}%)");
        }

        private static SurveyScore CesScore(List<int> values)
        {
            var average = Math.Round((decimal)values.Average(), 2);
            return new SurveyScore(SurveyType.CES, values.Count, average, $"Prosečan napor {average} od 7 (niže je bolje)");
        }

        [GeneratedRegex("^[a-z0-9:_-]{2,60}$")]
        private static partial Regex ContextPattern();
    }

    public class FaqService : IFaqService
    {
        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;

        public FaqService(ApplicationDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<FaqArticle>> GetPublishedAsync() =>
            await _context.FaqArticles.AsNoTracking()
                .Where(f => f.IsPublished)
                .OrderBy(f => f.Category).ThenBy(f => f.SortOrder)
                .ToListAsync();

        public async Task<IReadOnlyList<FaqArticle>> GetAllAsync() =>
            await _context.FaqArticles.AsNoTracking().OrderBy(f => f.Category).ThenBy(f => f.SortOrder).ToListAsync();

        public async Task<FaqArticle> SaveAsync(int? articleId, FaqArticleCommand command, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();

            var article = articleId.HasValue
                ? await _context.FaqArticles.FirstOrDefaultAsync(f => f.Id == articleId) ?? throw new NotFoundException("Pitanje nije pronađeno.")
                : new FaqArticle();

            article.Category = command.Category.Trim();
            article.Question = command.Question.Trim();
            article.Answer = command.Answer.Trim();
            article.SortOrder = command.SortOrder;
            article.IsPublished = command.IsPublished;
            article.UpdatedByUserId = actor.UserId;
            article.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

            if (!articleId.HasValue)
                await _context.FaqArticles.AddAsync(article);

            await _context.SaveChangesAsync();
            return article;
        }

        public async Task DeleteAsync(int articleId, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();
            var article = await _context.FaqArticles.FirstOrDefaultAsync(f => f.Id == articleId)
                ?? throw new NotFoundException("Pitanje nije pronađeno.");
            _context.FaqArticles.Remove(article);
            await _context.SaveChangesAsync();
        }
    }

    public class DataAccessLogger : IDataAccessLogger
    {
        private readonly ApplicationDbContext _context;
        private readonly IRequestContext _requestContext;
        private readonly TimeProvider _timeProvider;

        public DataAccessLogger(ApplicationDbContext context, IRequestContext requestContext, TimeProvider timeProvider)
        {
            _context = context;
            _requestContext = requestContext;
            _timeProvider = timeProvider;
        }

        public Task LogAsync(Actor viewer, int subjectUserId, string resource, int? resourceId, string purpose)
        {
            if (!viewer.IsStaff || viewer.UserId == subjectUserId)
                return Task.CompletedTask;

            return _context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO data_access_logs (ViewerUserId, SubjectUserId, Resource, ResourceId, Purpose, IpAddress, AccessedAt)
                VALUES ({viewer.UserId}, {subjectUserId}, {resource}, {resourceId}, {purpose}, {_requestContext.IpAddress},
                        {_timeProvider.GetUtcNow().UtcDateTime})");
        }

        public Task<PagedResult<DataAccessLog>> GetForSubjectAsync(int subjectUserId, PageRequest page) =>
            _context.DataAccessLogs.AsNoTracking()
                .Include(d => d.Viewer)
                .Where(d => d.SubjectUserId == subjectUserId)
                .OrderByDescending(d => d.AccessedAt)
                .ThenByDescending(d => d.Id)
                .ToPagedResultAsync(page);
    }

    public class PrivacyRequestService : IPrivacyRequestService
    {
        private const int MaxOpenRequests = 5;

        private readonly ApplicationDbContext _context;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly CustomerProtectionSettings _settings;
        private readonly TimeProvider _timeProvider;

        public PrivacyRequestService(
            ApplicationDbContext context,
            INotificationPublisher notificationPublisher,
            IOptions<CustomerProtectionSettings> settings,
            TimeProvider timeProvider)
        {
            _context = context;
            _notificationPublisher = notificationPublisher;
            _settings = settings.Value;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<PrivacyRequest> SubmitAsync(int userId, PrivacyRequestType type, string details)
        {
            var open = await _context.PrivacyRequests.CountAsync(r =>
                r.UserId == userId && (r.Status == PrivacyRequestStatus.Podnet || r.Status == PrivacyRequestStatus.UObradi));
            if (open >= MaxOpenRequests)
                throw new BusinessRuleException("Imate previše otvorenih zahteva. Sačekajte odgovor na postojeće.");

            var request = new PrivacyRequest
            {
                ReferenceNumber = $"ZZP-{UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}",
                UserId = userId,
                RequestType = type,
                Details = details.Trim(),
                Status = PrivacyRequestStatus.Podnet,
                DueAt = UtcNow.AddDays(_settings.PrivacyRequestResponseDays),
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };

            await _context.PrivacyRequests.AddAsync(request);
            await _notificationPublisher.PublishAsync(new Notification
            {
                UserId = userId,
                Type = NotificationType.Privatnost,
                Title = "Zahtev u vezi sa podacima je primljen",
                Message = $"Zahtev {request.ReferenceNumber} je zaveden. Odgovor ćete dobiti najkasnije do {request.DueAt:dd.MM.yyyy}.",
                CreatedAt = UtcNow
            });
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task<IReadOnlyList<PrivacyRequest>> GetMineAsync(int userId) =>
            await _context.PrivacyRequests.AsNoTracking()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

        public Task<PagedResult<PrivacyRequest>> GetPagedAsync(PrivacyRequestStatus? status, PageRequest page, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();

            var query = _context.PrivacyRequests.AsNoTracking().Include(r => r.User).AsQueryable();
            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);
            return query.OrderBy(r => r.DueAt).ToPagedResultAsync(page);
        }

        public async Task<PrivacyRequest> RespondAsync(int requestId, PrivacyRequestStatus status, string response, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();
            if (status is PrivacyRequestStatus.Podnet)
                throw new BusinessRuleException("Neispravan status odgovora.");

            var request = await _context.PrivacyRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == requestId)
                ?? throw new NotFoundException("Zahtev nije pronađen.");

            if (request.RespondedAt != null)
                throw new BusinessRuleException("Na zahtev je već odgovoreno.");

            request.Status = status;
            request.HandledByUserId = actor.UserId;
            request.UpdatedAt = UtcNow;

            if (status != PrivacyRequestStatus.UObradi)
            {
                if (string.IsNullOrWhiteSpace(response))
                    throw new BusinessRuleException("Odgovor korisniku je obavezan.");
                request.Response = response.Trim();
                request.RespondedAt = UtcNow;

                await _notificationPublisher.PublishAsync(new Notification
                {
                    UserId = request.UserId,
                    Type = NotificationType.Privatnost,
                    Title = "Odgovor na zahtev u vezi sa podacima",
                    Message = $"Na vaš zahtev {request.ReferenceNumber} je odgovoreno. Ako niste zadovoljni, možete se obratiti Povereniku za informacije od javnog značaja i zaštitu podataka o ličnosti.",
                    CreatedAt = UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return request;
        }
    }

    public class PreferencesService : IPreferencesService
    {
        private const string MarketingConsentType = "Marketing";

        private readonly ApplicationDbContext _context;
        private readonly IRequestContext _requestContext;
        private readonly TimeProvider _timeProvider;

        public PreferencesService(ApplicationDbContext context, IRequestContext requestContext, TimeProvider timeProvider)
        {
            _context = context;
            _requestContext = requestContext;
            _timeProvider = timeProvider;
        }

        public async Task<UserPreferences> GetAsync(int userId) =>
            await _context.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId)
            ?? new UserPreferences { UserId = userId };

        public async Task<UserPreferences> UpdateAsync(int userId, UpdatePreferencesCommand command)
        {
            var preferences = await _context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
            var isNew = preferences == null;
            preferences ??= new UserPreferences { UserId = userId };
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (preferences.MarketingConsent != command.MarketingConsent || isNew && command.MarketingConsent)
            {
                await _context.ConsentEvents.AddAsync(new ConsentEvent
                {
                    UserId = userId,
                    ConsentType = MarketingConsentType,
                    Granted = command.MarketingConsent,
                    Source = "Podesavanja",
                    IpAddress = _requestContext.IpAddress,
                    CreatedAt = now
                });
            }

            preferences.NotifyIncomingPayments = command.NotifyIncomingPayments;
            preferences.NotifyOutgoingPayments = command.NotifyOutgoingPayments;
            preferences.NotifyAccountChanges = command.NotifyAccountChanges;
            preferences.NotifyComplaintUpdates = command.NotifyComplaintUpdates;
            preferences.MarketingConsent = command.MarketingConsent;
            preferences.HideBalancesByDefault = command.HideBalancesByDefault;
            preferences.ExtraConfirmationThreshold = command.ExtraConfirmationThreshold;
            preferences.UpdatedAt = now;

            if (isNew)
                await _context.UserPreferences.AddAsync(preferences);

            await _context.SaveChangesAsync();
            return preferences;
        }

        public async Task<IReadOnlyList<ConsentEvent>> GetConsentHistoryAsync(int userId) =>
            await _context.ConsentEvents.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
    }

    public class DataRetentionService : IDataRetentionService
    {
        private readonly ApplicationDbContext _context;
        private readonly RetentionSettings _settings;
        private readonly TimeProvider _timeProvider;

        public DataRetentionService(ApplicationDbContext context, IOptions<RetentionSettings> settings, TimeProvider timeProvider)
        {
            _context = context;
            _settings = settings.Value;
            _timeProvider = timeProvider;
        }

        public async Task<RetentionReport> PurgeExpiredAsync(CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var notificationCutoff = now.AddDays(-_settings.ReadNotificationsDays);
            var sessionCutoff = now.AddDays(-_settings.ExpiredSessionsDays);

            var notifications = await _context.Notifications
                .Where(n => n.IsRead && n.CreatedAt < notificationCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            var sessions = await _context.UserSessions
                .Where(s => s.ExpiresAt < sessionCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            return new RetentionReport(notifications, sessions);
        }
    }
}
