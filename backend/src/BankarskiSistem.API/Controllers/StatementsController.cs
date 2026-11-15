using System.Text;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankarskiSistem.API.Controllers
{
    [Route(RoutePrefix + "/accounts/{accountId:int}/statement")]
    public class StatementsController(
        IStatementService statementService,
        IDataAccessLogger dataAccessLogger,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<AccountStatement>> Get(int accountId, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            var statement = await LoadAsync(accountId, from, to);
            return Ok(statement);
        }

        [HttpGet("csv")]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        public async Task<IActionResult> GetCsv(int accountId, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            var statement = await LoadAsync(accountId, from, to);
            await auditLog.LogAsync(new AuditEntry(CurrentActor.UserId, "Preuzet izvod", "Account", accountId,
                $"{statement.From:yyyy-MM-dd} do {statement.To:yyyy-MM-dd}, CSV"));
            var fileName = $"izvod-{statement.AccountNumber}-{statement.From:yyyyMMdd}-{statement.To:yyyyMMdd}.csv";
            return File(Encoding.UTF8.GetBytes(StatementCsv.Write(statement)), "text/csv; charset=utf-8", fileName);
        }

        private async Task<AccountStatement> LoadAsync(int accountId, DateOnly from, DateOnly to)
        {
            var actor = CurrentActor;
            var statement = await statementService.GetAsync(accountId, from, to, actor);
            if (actor.IsStaff)
                await dataAccessLogger.LogAsync(actor, statement.OwnerUserId, "Izvod", accountId, "Izrada izvoda po računu");
            return statement;
        }
    }
}
