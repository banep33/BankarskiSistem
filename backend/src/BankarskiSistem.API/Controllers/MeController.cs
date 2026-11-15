using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankarskiSistem.API.Controllers
{
    [Route(RoutePrefix + "/me")]
    public class MeController(
        IUserService userService,
        IAuthService authService,
        ILegalService legalService,
        IAccountService accountService,
        ITransactionService transactionService,
        IStatisticsService statisticsService,
        IBusinessCalendar businessCalendar,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<ProfileResponse>> GetProfile()
        {
            var userId = CurrentActor.UserId;
            var user = await userService.GetProfileAsync(userId);
            var pending = await legalService.GetPendingDocumentsAsync(userId);
            return Ok(new ProfileResponse(user.ToResponse(), pending.Select(d => d.ToSummary()).ToList()));
        }

        [HttpPut]
        public async Task<ActionResult<UserResponse>> UpdateProfile(UpdateProfileRequest request)
        {
            var userId = CurrentActor.UserId;
            var user = await userService.UpdateProfileAsync(userId, new UpdateProfileCommand(request.FirstName, request.LastName));
            await auditLog.LogAsync(new AuditEntry(userId, "Izmenjen profil", "User", userId, user.FullName));
            return Ok(user.ToResponse());
        }

        [HttpPut("password")]
        public async Task<ActionResult<LoginResponse>> ChangePassword(ChangePasswordRequest request)
        {
            var userId = CurrentActor.UserId;
            var sessionId = User.FindSessionId() ?? throw new UnauthorizedAccessException();
            var result = await authService.ChangePasswordAsync(userId, sessionId, request.CurrentPassword, request.NewPassword);
            await auditLog.LogAsync(new AuditEntry(userId, "Promena lozinke", "User", userId));
            var pending = await legalService.GetPendingDocumentsAsync(userId);
            return Ok(result.ToResponse(pending));
        }

        [HttpGet("dashboard")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<CustomerDashboardResponse>> GetDashboard()
        {
            var dashboard = await statisticsService.GetCustomerDashboardAsync(CurrentActor.UserId);
            return Ok(dashboard.ToResponse());
        }

        [HttpGet("accounts")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<IEnumerable<AccountResponse>>> GetAccounts()
        {
            var actor = CurrentActor;
            var accounts = await accountService.GetByUserIdAsync(actor.UserId, actor);
            return Ok(accounts.Select(a => a.ToResponse()));
        }

        [HttpGet("transactions")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<CursorResponse<TransactionResponse>>> GetTransactions([FromQuery] TransactionCursorQuery query)
        {
            var (fromUtc, toUtc) = ToUtcRange(businessCalendar, query.From, query.To);
            var filter = new TransactionFilter
            {
                AccountId = query.AccountId,
                Type = query.Type,
                Status = query.Status,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Search = query.Search
            };

            var page = await transactionService.GetCursorPageAsync(filter, query.Cursor, query.Limit, CurrentActor);
            return Ok(page.ToResponse(t => t.ToResponse()));
        }
    }

    [Route(RoutePrefix + "/me/notifications")]
    public class MeNotificationsController(INotificationService notificationService) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<PagedResponse<NotificationResponse>>> GetAll(
            [FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await notificationService.GetPagedAsync(CurrentActor.UserId, unreadOnly, new PageRequest(page, pageSize));
            return Ok(result.ToResponse(n => n.ToResponse()));
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount() =>
            Ok(new UnreadCountResponse(await notificationService.CountUnreadAsync(CurrentActor.UserId)));

        [HttpPost("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await notificationService.MarkAsReadAsync(CurrentActor.UserId, id);
            return NoContent();
        }

        [HttpPost("read-all")]
        public async Task<ActionResult<UnreadCountResponse>> MarkAllAsRead()
        {
            await notificationService.MarkAllAsReadAsync(CurrentActor.UserId);
            return Ok(new UnreadCountResponse(0));
        }
    }

    [Route(RoutePrefix + "/me/beneficiaries")]
    [Authorize(Policy = Policies.Customer)]
    public class MeBeneficiariesController(IBeneficiaryService beneficiaryService) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BeneficiaryResponse>>> GetAll() =>
            Ok((await beneficiaryService.GetAllAsync(CurrentActor.UserId)).Select(b => b.ToResponse()));

        [HttpPost]
        public async Task<ActionResult<BeneficiaryResponse>> Create(BeneficiaryRequest request)
        {
            var beneficiary = await beneficiaryService.CreateAsync(CurrentActor.UserId, ToCommand(request));
            return StatusCode(StatusCodes.Status201Created, beneficiary.ToResponse());
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<BeneficiaryResponse>> Update(int id, BeneficiaryRequest request) =>
            Ok((await beneficiaryService.UpdateAsync(CurrentActor.UserId, id, ToCommand(request))).ToResponse());

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await beneficiaryService.DeleteAsync(CurrentActor.UserId, id);
            return NoContent();
        }

        private static BeneficiaryCommand ToCommand(BeneficiaryRequest request) =>
            new(request.Nickname, request.RecipientName, request.AccountNumber, request.RecipientAddress,
                request.PaymentCode, request.Purpose, request.ReferenceModel, request.ReferenceNumber);
    }

    [Route(RoutePrefix + "/me/privacy")]
    public class MePrivacyController(
        ILegalService legalService,
        IPrivacyService privacyService,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet("consents")]
        public async Task<ActionResult<IEnumerable<ConsentResponse>>> GetConsents() =>
            Ok((await legalService.GetConsentsAsync(CurrentActor.UserId)).Select(c => c.ToResponse()));

        [HttpGet("pending-documents")]
        public async Task<ActionResult<IEnumerable<LegalDocumentResponse>>> GetPendingDocuments() =>
            Ok((await legalService.GetPendingDocumentsAsync(CurrentActor.UserId)).Select(d => d.ToResponse()));

        [HttpPost("consents")]
        public async Task<IActionResult> Accept(AcceptDocumentsRequest request)
        {
            var userId = CurrentActor.UserId;
            await legalService.AcceptAsync(userId, request.DocumentIds);
            await auditLog.LogAsync(new AuditEntry(userId, "Prihvaceni pravni dokumenti", "LegalDocument",
                NewValue: string.Join(", ", request.DocumentIds)));
            return NoContent();
        }

        [HttpGet("security-events")]
        public async Task<ActionResult<PagedResponse<SecurityEvent>>> GetSecurityEvents(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await privacyService.GetSecurityEventsAsync(CurrentActor.UserId, new PageRequest(page, pageSize));
            return Ok(result.ToResponse(e => e));
        }

        [HttpGet("data-export")]
        [RequireRecentAuthentication]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        public async Task<IActionResult> ExportData()
        {
            var userId = CurrentActor.UserId;
            var export = await privacyService.ExportAsync(userId);
            await auditLog.LogAsync(new AuditEntry(userId, "Izvoz licnih podataka", "User", userId));
            Response.Headers.ContentDisposition = $"attachment; filename=\"moji-podaci-{export.GeneratedAt:yyyyMMdd-HHmm}.json\"";
            return Ok(export);
        }
    }
}
