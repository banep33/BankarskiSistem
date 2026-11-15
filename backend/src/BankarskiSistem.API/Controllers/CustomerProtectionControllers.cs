using BankarskiSistem.API.BackgroundJobs;
using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.API.Controllers
{
    [Route(RoutePrefix + "/me/security")]
    public class MeSecurityController(
        ISessionService sessionService,
        IPrivacyService privacyService,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet("sessions")]
        public async Task<ActionResult<IEnumerable<SessionResponse>>> GetSessions()
        {
            var current = User.FindSessionId() ?? Guid.Empty;
            var sessions = await sessionService.GetActiveAsync(CurrentActor.UserId);
            return Ok(sessions.Select(s => s.ToResponse(current)));
        }

        [HttpDelete("sessions/{sessionId:guid}")]
        public async Task<IActionResult> RevokeSession(Guid sessionId)
        {
            var userId = CurrentActor.UserId;
            await sessionService.RevokeAsync(userId, sessionId, "Odjavljeno od strane korisnika");
            await auditLog.LogAsync(new AuditEntry(userId, "Odjavljena sesija", "UserSession", NewValue: sessionId.ToString()));
            return NoContent();
        }

        [HttpPost("sessions/revoke-others")]
        public async Task<IActionResult> RevokeOtherSessions()
        {
            var userId = CurrentActor.UserId;
            var current = User.FindSessionId() ?? throw new UnauthorizedAccessException();
            var count = await sessionService.RevokeOthersAsync(userId, current, "Odjavljeni svi ostali uređaji");
            await auditLog.LogAsync(new AuditEntry(userId, "Odjavljeni ostali uredjaji", "UserSession", NewValue: $"Broj sesija: {count}"));
            return Ok(new { revoked = count });
        }

        [HttpPost("reauthenticate")]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        public async Task<IActionResult> Reauthenticate(ReauthenticateRequest request)
        {
            var userId = CurrentActor.UserId;
            var current = User.FindSessionId() ?? throw new UnauthorizedAccessException();
            try
            {
                await sessionService.ReauthenticateAsync(userId, current, request.Password);
                await auditLog.LogAsync(new AuditEntry(userId, "Potvrda lozinkom", "UserSession"));
            }
            catch
            {
                await auditLog.LogAsync(new AuditEntry(userId, "Potvrda lozinkom", "UserSession", IsSuccess: false, ErrorMessage: "Pogresna lozinka"));
                throw;
            }
            return NoContent();
        }

        [HttpGet("events")]
        public async Task<ActionResult<PagedResponse<SecurityEvent>>> GetEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await privacyService.GetSecurityEventsAsync(CurrentActor.UserId, new PageRequest(page, pageSize));
            return Ok(result.ToResponse(e => e));
        }
    }

    [Route(RoutePrefix + "/me/preferences")]
    public class MePreferencesController(IPreferencesService preferencesService, IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<PreferencesResponse>> Get() =>
            Ok((await preferencesService.GetAsync(CurrentActor.UserId)).ToResponse());

        [HttpPut]
        public async Task<ActionResult<PreferencesResponse>> Update(UpdatePreferencesRequest request)
        {
            var userId = CurrentActor.UserId;
            var preferences = await preferencesService.UpdateAsync(userId, new UpdatePreferencesCommand(
                request.NotifyIncomingPayments, request.NotifyOutgoingPayments, request.NotifyAccountChanges,
                request.NotifyComplaintUpdates, request.MarketingConsent, request.HideBalancesByDefault,
                request.ExtraConfirmationThreshold));
            await auditLog.LogAsync(new AuditEntry(userId, "Izmenjena podesavanja", "UserPreferences", userId));
            return Ok(preferences.ToResponse());
        }

        [HttpGet("consent-history")]
        public async Task<ActionResult<IEnumerable<ConsentEventResponse>>> GetConsentHistory() =>
            Ok((await preferencesService.GetConsentHistoryAsync(CurrentActor.UserId))
                .Select(c => new ConsentEventResponse(c.ConsentType, c.Granted, c.Source, c.CreatedAt)));
    }

    [Route(RoutePrefix + "/me/privacy")]
    public class MePrivacyRequestsController(
        IDataAccessLogger dataAccessLogger,
        IPrivacyRequestService privacyRequestService,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet("data-access")]
        public async Task<ActionResult<PagedResponse<DataAccessResponse>>> GetDataAccess([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await dataAccessLogger.GetForSubjectAsync(CurrentActor.UserId, new PageRequest(page, pageSize));
            return Ok(result.ToResponse(log => log.ToResponse()));
        }

        [HttpGet("requests")]
        public async Task<ActionResult<IEnumerable<PrivacyRequestResponse>>> GetRequests() =>
            Ok((await privacyRequestService.GetMineAsync(CurrentActor.UserId)).Select(r => r.ToResponse()));

        [HttpPost("requests")]
        [EnableRateLimiting(RateLimitPolicies.Submissions)]
        public async Task<ActionResult<PrivacyRequestResponse>> SubmitRequest(SubmitPrivacyRequest request)
        {
            var userId = CurrentActor.UserId;
            var created = await privacyRequestService.SubmitAsync(userId, request.RequestType, request.Details);
            await auditLog.LogAsync(new AuditEntry(userId, "Podnet zahtev za zastitu podataka", "PrivacyRequest", created.Id, request.RequestType.ToString()));
            return StatusCode(StatusCodes.Status201Created, created.ToResponse());
        }
    }

    [Route(RoutePrefix + "/me/complaints")]
    public class MeComplaintsController(IComplaintService complaintService, TimeProvider timeProvider, IAuditLogService auditLog) : ApiControllerBase
    {
        private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

        [HttpGet]
        public async Task<ActionResult<PagedResponse<ComplaintSummaryResponse>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await complaintService.GetMineAsync(CurrentActor.UserId, new PageRequest(page, pageSize));
            return Ok(result.ToResponse(c => c.ToSummary(UtcNow)));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ComplaintDetailResponse>> GetById(int id) =>
            Ok((await complaintService.GetMineAsync(CurrentActor.UserId, id)).ToDetail(UtcNow));

        [HttpPost]
        [EnableRateLimiting(RateLimitPolicies.Submissions)]
        public async Task<ActionResult<ComplaintDetailResponse>> Submit(SubmitComplaintRequest request)
        {
            var userId = CurrentActor.UserId;
            var complaint = await complaintService.SubmitAsync(userId, new SubmitComplaintCommand(
                request.Category, request.Subject, request.Description, request.RelatedTransactionId, request.RelatedAccountId));
            await auditLog.LogAsync(new AuditEntry(userId, "Podnet prigovor", "Complaint", complaint.Id, complaint.ReferenceNumber));
            return CreatedAtAction(nameof(GetById), new { id = complaint.Id },
                (await complaintService.GetMineAsync(userId, complaint.Id)).ToDetail(UtcNow));
        }

        [HttpPost("{id:int}/messages")]
        public async Task<ActionResult<ComplaintDetailResponse>> AddMessage(int id, ComplaintMessageRequest request) =>
            Ok((await complaintService.AddCustomerMessageAsync(CurrentActor.UserId, id, request.Message)).ToDetail(UtcNow));
    }

    [Route(RoutePrefix + "/me/feedback")]
    public class MeFeedbackController(IFeedbackService feedbackService) : ApiControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<FeedbackResponse>> Submit(SubmitFeedbackRequest request)
        {
            var feedback = await feedbackService.SubmitAsync(CurrentActor.UserId,
                new SubmitFeedbackCommand(request.SurveyType, request.Score, request.Comment, request.Context));
            return StatusCode(StatusCodes.Status201Created, feedback.ToResponse());
        }
    }

    [Authorize(Policy = Policies.Staff)]
    public class ComplaintsController(IComplaintService complaintService, TimeProvider timeProvider, IAuditLogService auditLog) : ApiControllerBase
    {
        private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

        [HttpGet]
        public async Task<ActionResult<PagedResponse<ComplaintSummaryResponse>>> GetAll([FromQuery] ComplaintQuery query)
        {
            var filter = new ComplaintFilter
            {
                Status = query.Status,
                AssignedToUserId = query.AssignedToUserId,
                OnlyOverdue = query.OnlyOverdue,
                OnlyUnassigned = query.OnlyUnassigned
            };
            var result = await complaintService.GetPagedAsync(filter, new PageRequest(query.Page, query.PageSize), CurrentActor);
            return Ok(result.ToResponse(c => c.ToSummary(UtcNow)));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ComplaintDetailResponse>> GetById(int id) =>
            Ok((await complaintService.GetForStaffAsync(id, CurrentActor)).ToDetail(UtcNow));

        [HttpPost("{id:int}/assign")]
        public Task<ActionResult<ComplaintDetailResponse>> Assign(int id, AssignComplaintRequest request) =>
            AuditedAsync(id, "Dodeljen prigovor", $"Dodeljeno korisniku {request.AssigneeUserId}",
                actor => complaintService.AssignAsync(id, request.AssigneeUserId, actor));

        [HttpPost("{id:int}/status")]
        public Task<ActionResult<ComplaintDetailResponse>> ChangeStatus(int id, ChangeComplaintStatusRequest request) =>
            AuditedAsync(id, "Promenjen status prigovora", request.Status.ToString(),
                actor => complaintService.ChangeStatusAsync(id, request.Status, request.Note, actor));

        [HttpPost("{id:int}/extend-deadline")]
        public Task<ActionResult<ComplaintDetailResponse>> ExtendDeadline(int id, ExtendComplaintDeadlineRequest request) =>
            AuditedAsync(id, "Produzen rok prigovora", request.Reason,
                actor => complaintService.ExtendDeadlineAsync(id, request.Reason, actor));

        [HttpPost("{id:int}/messages")]
        public Task<ActionResult<ComplaintDetailResponse>> AddMessage(int id, ComplaintMessageRequest request) =>
            AuditedAsync(id, request.IsInternalNote ? "Interna beleska na prigovoru" : "Odgovor na prigovor", null,
                actor => complaintService.AddStaffMessageAsync(id, request.Message, request.IsInternalNote, actor));

        private async Task<ActionResult<ComplaintDetailResponse>> AuditedAsync(
            int id, string action, string? details, Func<Actor, Task<Core.Entities.Complaint>> operation)
        {
            var actor = CurrentActor;
            var complaint = await operation(actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, action, "Complaint", id, details));
            return Ok(complaint.ToDetail(UtcNow));
        }
    }

    [AllowAnonymous]
    public class HelpController(IFaqService faqService, IFeeService feeService, IOptions<SupportContactOptions> support) : ApiControllerBase
    {
        [HttpGet("faq")]
        public async Task<ActionResult<IEnumerable<FaqResponse>>> GetFaq() =>
            Ok((await faqService.GetPublishedAsync()).Select(f => f.ToResponse()));

        [HttpGet("contact")]
        public ActionResult<SupportContactResponse> GetContact() =>
            Ok(new SupportContactResponse(support.Value.Phone, support.Value.Email, support.Value.WorkingHours, support.Value.RegulatorInfo));

        [HttpGet("fees")]
        public async Task<ActionResult<IEnumerable<FeeResponse>>> GetFees() =>
            Ok((await feeService.GetCurrentScheduleAsync()).Select(f => f.ToResponse()));
    }

    [Route(RoutePrefix + "/admin")]
    public class AdminCustomerProtectionController(
        IFeedbackService feedbackService,
        IFaqService faqService,
        IComplianceService complianceService,
        IPrivacyRequestService privacyRequestService,
        IDataRetentionService dataRetentionService,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet("feedback/summary")]
        [Authorize(Policy = Policies.Staff)]
        public async Task<ActionResult<FeedbackSummary>> GetFeedbackSummary([FromQuery] int days = 90) =>
            Ok(await feedbackService.GetSummaryAsync(DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 3650))));

        [HttpGet("feedback")]
        [Authorize(Policy = Policies.Staff)]
        public async Task<ActionResult<PagedResponse<FeedbackResponse>>> GetFeedback(
            [FromQuery] SurveyType? type, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
            Ok((await feedbackService.GetRecentAsync(type, new PageRequest(page, pageSize))).ToResponse(f => f.ToResponse()));

        [HttpGet("faq")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<IEnumerable<FaqResponse>>> GetAllFaq() =>
            Ok((await faqService.GetAllAsync()).Select(f => f.ToResponse()));

        [HttpPost("faq")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<FaqResponse>> CreateFaq(FaqArticleRequest request) =>
            StatusCode(StatusCodes.Status201Created, (await faqService.SaveAsync(null, ToCommand(request), CurrentActor)).ToResponse());

        [HttpPut("faq/{id:int}")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<FaqResponse>> UpdateFaq(int id, FaqArticleRequest request) =>
            Ok((await faqService.SaveAsync(id, ToCommand(request), CurrentActor)).ToResponse());

        [HttpDelete("faq/{id:int}")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<IActionResult> DeleteFaq(int id)
        {
            await faqService.DeleteAsync(id, CurrentActor);
            return NoContent();
        }

        [HttpGet("compliance/alerts")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<PagedResponse<ComplianceAlertResponse>>> GetAlerts(
            [FromQuery] ComplianceAlertStatus? status, [FromQuery] RiskLevel? severity, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
            Ok((await complianceService.GetPagedAsync(status, severity, new PageRequest(page, pageSize), CurrentActor)).ToResponse(a => a.ToResponse()));

        [HttpPost("compliance/alerts/{id:int}/review")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<ComplianceAlertResponse>> ReviewAlert(int id, ReviewComplianceAlertRequest request)
        {
            var actor = CurrentActor;
            var alert = await complianceService.ReviewAsync(id, request.Status, request.Note, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Pregledano AML upozorenje", "ComplianceAlert", id, request.Status.ToString()));
            return Ok(alert.ToResponse());
        }

        [HttpGet("privacy-requests")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<PagedResponse<PrivacyRequestResponse>>> GetPrivacyRequests(
            [FromQuery] PrivacyRequestStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
            Ok((await privacyRequestService.GetPagedAsync(status, new PageRequest(page, pageSize), CurrentActor)).ToResponse(r => r.ToResponse()));

        [HttpPost("privacy-requests/{id:int}/respond")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<PrivacyRequestResponse>> RespondPrivacyRequest(int id, RespondPrivacyRequest request)
        {
            var actor = CurrentActor;
            var updated = await privacyRequestService.RespondAsync(id, request.Status, request.Response, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Odgovor na zahtev za zastitu podataka", "PrivacyRequest", id, request.Status.ToString()));
            return Ok(updated.ToResponse());
        }

        [HttpPost("retention/run")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<RetentionReport>> RunRetention(CancellationToken cancellationToken)
        {
            var report = await dataRetentionService.PurgeExpiredAsync(cancellationToken);
            await auditLog.LogAsync(new AuditEntry(CurrentActor.UserId, "Pokrenuto ciscenje po rokovima cuvanja", "Retention",
                NewValue: $"Obavestenja: {report.ReadNotificationsDeleted}, sesije: {report.ExpiredSessionsDeleted}"));
            return Ok(report);
        }

        private static FaqArticleCommand ToCommand(FaqArticleRequest request) =>
            new(request.Category, request.Question, request.Answer, request.SortOrder, request.IsPublished);
    }
}
