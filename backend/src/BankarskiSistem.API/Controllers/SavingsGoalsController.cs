using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankarskiSistem.API.Controllers
{
    [Route(RoutePrefix + "/me/savings-goals")]
    [Authorize(Policy = Policies.Customer)]
    public class SavingsGoalsController(
        ISavingsGoalService savingsGoalService,
        IBusinessCalendar businessCalendar,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SavingsGoalResponse>>> GetMine() =>
            Ok((await savingsGoalService.GetMineAsync(CurrentActor)).Select(g => g.ToResponse(businessCalendar.Today())));

        [HttpPost]
        [RequireAcceptedTerms]
        public async Task<ActionResult<SavingsGoalResponse>> Create(CreateSavingsGoalRequest request)
        {
            var actor = CurrentActor;
            var goal = await savingsGoalService.CreateAsync(actor, request.Name, request.TargetAmount, request.TargetDate, request.Currency);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Otvoren cilj stednje", "SavingsGoal", goal.Id, $"{goal.Name}, racun {goal.Account?.AccountNumber}"));
            return StatusCode(StatusCodes.Status201Created, goal.ToResponse(businessCalendar.Today()));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<SavingsGoalResponse>> Update(int id, UpdateSavingsGoalRequest request) =>
            Ok((await savingsGoalService.UpdateAsync(CurrentActor, id, request.Name, request.TargetAmount, request.TargetDate)).ToResponse(businessCalendar.Today()));

        [HttpPost("{id:int}/contributions")]
        [RequireAcceptedTerms]
        [EnableRateLimiting(RateLimitPolicies.Payments)]
        public async Task<ActionResult<SavingsGoalResponse>> Contribute(
            int id, SavingsMovementRequest request, [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey)
        {
            var actor = CurrentActor;
            var goal = await savingsGoalService.ContributeAsync(actor, id, request.AccountId, request.Amount, NormalizeIdempotencyKey(idempotencyKey));
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Uplata na cilj stednje", "SavingsGoal", id, $"{request.Amount}"));
            return Ok(goal.ToResponse(businessCalendar.Today()));
        }

        [HttpPost("{id:int}/withdrawals")]
        [EnableRateLimiting(RateLimitPolicies.Payments)]
        public async Task<ActionResult<SavingsGoalResponse>> Withdraw(
            int id, SavingsMovementRequest request, [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey)
        {
            var actor = CurrentActor;
            var goal = await savingsGoalService.WithdrawAsync(actor, id, request.AccountId, request.Amount, NormalizeIdempotencyKey(idempotencyKey));
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Povlacenje sa cilja stednje", "SavingsGoal", id, $"{request.Amount}"));
            return Ok(goal.ToResponse(businessCalendar.Today()));
        }

        [HttpPost("{id:int}/close")]
        public async Task<ActionResult<SavingsGoalResponse>> Close(int id, CloseSavingsGoalRequest request)
        {
            var actor = CurrentActor;
            var goal = await savingsGoalService.CloseAsync(actor, id, request.AccountId);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Zatvoren cilj stednje", "SavingsGoal", id));
            return Ok(goal.ToResponse(businessCalendar.Today()));
        }
    }
}
