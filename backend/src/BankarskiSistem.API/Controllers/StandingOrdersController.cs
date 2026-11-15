using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankarskiSistem.API.Controllers
{
    [Route(RoutePrefix + "/standing-orders")]
    [Authorize(Policy = Policies.Customer)]
    public class StandingOrdersController(IStandingOrderService standingOrderService, IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<StandingOrderResponse>>> GetMine() =>
            Ok((await standingOrderService.GetMineAsync(CurrentActor)).Select(o => o.ToResponse()));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<StandingOrderDetailResponse>> Get(int id) =>
            Ok((await standingOrderService.GetAsync(id, CurrentActor)).ToDetailResponse());

        [HttpPost]
        [RequireAcceptedTerms]
        [RequireRecentAuthentication]
        [EnableRateLimiting(RateLimitPolicies.Submissions)]
        public async Task<ActionResult<StandingOrderResponse>> Create(CreateStandingOrderRequest request)
        {
            var actor = CurrentActor;
            var order = await standingOrderService.CreateAsync(new CreateStandingOrderCommand(
                request.FromAccountId, request.RecipientName, request.RecipientAccountNumber, request.RecipientAddress, request.Amount,
                request.PaymentCode, request.Purpose, request.ReferenceModel, request.ReferenceNumber, request.Frequency,
                request.StartDate, request.EndDate, request.MaxExecutions), actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Kreiran trajni nalog", "StandingOrder", order.Id,
                $"{order.Amount} {order.Currency}, {order.Frequency}, od {order.StartDate:yyyy-MM-dd}, za {order.RecipientAccountNumber}"));
            return CreatedAtAction(nameof(Get), new { id = order.Id }, order.ToResponse());
        }

        [HttpPut("{id:int}")]
        [RequireRecentAuthentication]
        public async Task<ActionResult<StandingOrderResponse>> Update(int id, UpdateStandingOrderRequest request)
        {
            var actor = CurrentActor;
            var order = await standingOrderService.UpdateAsync(id, new UpdateStandingOrderCommand(
                request.Amount, request.Purpose, request.ReferenceModel, request.ReferenceNumber, request.EndDate, request.MaxExecutions), actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Izmenjen trajni nalog", "StandingOrder", id,
                $"{order.Amount} {order.Currency}, kraj {order.EndDate?.ToString("yyyy-MM-dd") ?? "bez kraja"}"));
            return Ok(order.ToResponse());
        }

        [HttpPost("{id:int}/pause")]
        public Task<ActionResult<StandingOrderResponse>> Pause(int id) =>
            ChangeAsync(id, "Pauziran trajni nalog", standingOrderService.PauseAsync);

        [HttpPost("{id:int}/resume")]
        public Task<ActionResult<StandingOrderResponse>> Resume(int id) =>
            ChangeAsync(id, "Nastavljen trajni nalog", standingOrderService.ResumeAsync);

        [HttpPost("{id:int}/cancel")]
        public Task<ActionResult<StandingOrderResponse>> Cancel(int id) =>
            ChangeAsync(id, "Otkazan trajni nalog", standingOrderService.CancelAsync);

        private async Task<ActionResult<StandingOrderResponse>> ChangeAsync(
            int id, string action, Func<int, Actor, Task<Core.Entities.StandingOrder>> operation)
        {
            var actor = CurrentActor;
            var order = await operation(id, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, action, "StandingOrder", id));
            return Ok(order.ToResponse());
        }
    }
}
