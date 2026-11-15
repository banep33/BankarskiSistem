using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankarskiSistem.API.Controllers
{
    [Authorize(Policy = Policies.Customer)]
    public class PaymentsController(
        IPaymentOrderService paymentOrderService,
        ISessionService sessionService,
        IBudgetService budgetService,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpPost("quote")]
        public async Task<ActionResult<PaymentQuote>> Quote(CreatePaymentRequest request) =>
            Ok(await paymentOrderService.QuoteAsync(ToCommand(request, null, false), CurrentActor));

        [HttpPost]
        [RequireAcceptedTerms]
        [EnableRateLimiting(RateLimitPolicies.Payments)]
        public async Task<ActionResult<PaymentOrderResponse>> Create(
            CreatePaymentRequest request, [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey)
        {
            var actor = CurrentActor;
            var sessionId = User.FindSessionId();
            var strongConfirmation = sessionId.HasValue && await sessionService.IsRecentlyReauthenticatedAsync(sessionId.Value);
            var result = await paymentOrderService.CreateAsync(
                ToCommand(request, NormalizeIdempotencyKey(idempotencyKey), strongConfirmation), actor);

            if (!result.IsReplay)
            {
                await auditLog.LogAsync(new AuditEntry(actor.UserId, "Kreiran platni nalog", "PaymentOrder", result.Order.Id,
                    $"{result.Order.Amount} {result.Order.Currency} za {result.Order.RecipientAccountNumber}, status {result.Order.Status}"));
            }

            if (!result.IsReplay)
                await budgetService.TryEvaluateAlertsAsync(actor.UserId);

            var response = result.Order.ToResponse(result.IsReplay);
            return result.IsReplay
                ? Ok(response)
                : CreatedAtAction(nameof(GetById), new { id = result.Order.Id }, response);
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<PaymentOrderResponse>>> GetAll([FromQuery] PaymentOrderQuery query)
        {
            var filter = new PaymentOrderFilter { Status = query.Status, AccountId = query.AccountId };
            var result = await paymentOrderService.GetPagedAsync(filter, new PageRequest(query.Page, query.PageSize), CurrentActor);
            return Ok(result.ToResponse(o => o.ToResponse()));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PaymentOrderResponse>> GetById(int id) =>
            Ok((await paymentOrderService.GetAsync(id, CurrentActor)).ToResponse());

        [HttpPost("{id:int}/cancel")]
        public async Task<ActionResult<PaymentOrderResponse>> Cancel(int id)
        {
            var actor = CurrentActor;
            var order = await paymentOrderService.CancelAsync(id, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Otkazan zakazani nalog", "PaymentOrder", id));
            return Ok(order.ToResponse());
        }

        private static CreatePaymentOrderCommand ToCommand(CreatePaymentRequest request, string? idempotencyKey, bool strongConfirmation) =>
            new(
                request.FromAccountId,
                request.RecipientName,
                request.RecipientAccountNumber,
                request.RecipientAddress,
                request.Amount,
                request.PaymentCode,
                request.Purpose,
                request.ReferenceModel,
                request.ReferenceNumber,
                request.ScheduledFor,
                request.SaveAsBeneficiary,
                idempotencyKey,
                strongConfirmation);
    }
}
