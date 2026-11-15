using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankarskiSistem.API.Controllers
{
    public class AccountsController(
        IAccountService accountService,
        IPaymentService paymentService,
        ITransactionService transactionService,
        IStatisticsService statisticsService,
        IBusinessCalendar businessCalendar,
        IDataAccessLogger dataAccessLogger,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<PagedResponse<AccountResponse>>> GetAll([FromQuery] AccountQuery query)
        {
            var result = await accountService.GetPagedAsync(query.Search, new PageRequest(query.Page, query.PageSize));
            return Ok(result.ToResponse(a => a.ToResponse()));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AccountResponse>> GetById(int id)
        {
            var actor = CurrentActor;
            var account = await accountService.GetByIdAsync(id, actor);
            await dataAccessLogger.LogAsync(actor, account.UserId, "Racun", id, "Pregled računa");
            return Ok(account.ToResponse());
        }

        [HttpGet("{id:int}/transactions")]
        public async Task<ActionResult<CursorResponse<TransactionResponse>>> GetTransactions(
            int id, [FromQuery] TransactionCursorQuery query)
        {
            var actor = CurrentActor;
            var account = await accountService.GetByIdAsync(id, actor);
            await dataAccessLogger.LogAsync(actor, account.UserId, "Transakcije", id, "Pregled transakcija računa");

            var (fromUtc, toUtc) = ToUtcRange(businessCalendar, query.From, query.To);
            var filter = new TransactionFilter
            {
                AccountId = id,
                Type = query.Type,
                Status = query.Status,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Search = query.Search
            };

            var page = await transactionService.GetCursorPageAsync(filter, query.Cursor, query.Limit, actor);
            return Ok(page.ToResponse(t => t.ToResponse()));
        }

        [HttpGet("{id:int}/statistics")]
        public async Task<ActionResult<AccountStatistics>> GetStatistics(int id, [FromQuery] StatisticsPeriod period = StatisticsPeriod.Week) =>
            Ok(await statisticsService.GetAccountStatisticsAsync(id, period, CurrentActor));

        [HttpPost]
        [Authorize(Policy = Policies.Staff)]
        public async Task<ActionResult<AccountResponse>> Create(CreateAccountRequest request)
        {
            var actor = CurrentActor;
            var account = await accountService.CreateAsync(request.UserId, request.Currency, actor);

            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Kreiran racun", "Account", account.Id,
                $"Racun {account.AccountNumber} ({account.Currency}) za korisnika ID {request.UserId}"));

            return CreatedAtAction(nameof(GetById), new { id = account.Id }, account.ToResponse());
        }

        [HttpPost("{id:int}/deposits")]
        [Authorize(Policy = Policies.Staff)]
        [EnableRateLimiting(RateLimitPolicies.Payments)]
        public async Task<ActionResult<FinancialOperationResponse>> Deposit(
            int id, MoneyRequest request, [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey)
        {
            var actor = CurrentActor;
            var result = await paymentService.DepositAsync(
                new DepositCommand(id, request.Amount, request.Description, NormalizeIdempotencyKey(idempotencyKey)), actor);

            if (!result.IsReplay)
                await auditLog.LogAsync(new AuditEntry(actor.UserId, "Uplata na racun", "Account", id,
                    $"{result.Transaction.Amount} {result.Transaction.Currency}, ref {result.Transaction.ReferenceNumber}"));

            return Ok(result.ToResponse("Uplata je uspešno izvršena."));
        }

        [HttpPost("{id:int}/withdrawals")]
        [Authorize(Policy = Policies.Customer)]
        [RequireAcceptedTerms]
        [EnableRateLimiting(RateLimitPolicies.Payments)]
        public async Task<ActionResult<FinancialOperationResponse>> Withdraw(
            int id, MoneyRequest request, [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey)
        {
            var actor = CurrentActor;
            var result = await paymentService.WithdrawAsync(
                new WithdrawCommand(id, request.Amount, request.Description, NormalizeIdempotencyKey(idempotencyKey)), actor);

            if (!result.IsReplay)
                await auditLog.LogAsync(new AuditEntry(actor.UserId, "Isplata sa racuna", "Account", id,
                    $"{result.Transaction.Amount} {result.Transaction.Currency}, ref {result.Transaction.ReferenceNumber}"));

            return Ok(result.ToResponse("Isplata je uspešno izvršena."));
        }

        [HttpPut("{id:int}/status")]
        [Authorize(Policy = Policies.Staff)]
        public async Task<ActionResult<AccountResponse>> ChangeStatus(int id, ChangeAccountStatusRequest request)
        {
            var actor = CurrentActor;
            var account = await accountService.ChangeStatusAsync(id, request.Status, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Promenjen status racuna", "Account", id, $"Novi status: {request.Status}"));
            return Ok(account.ToResponse());
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<IActionResult> Delete(int id)
        {
            var actor = CurrentActor;
            await accountService.DeleteAsync(id, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Obrisan racun", "Account", id));
            return NoContent();
        }
    }
}
