using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.API.Controllers
{
    public class AdminController(
        IStatisticsService statisticsService,
        IAuditLogService auditLogService,
        ILedgerQueryService ledgerQueryService,
        ILegalService legalService,
        IBusinessCalendar businessCalendar) : ApiControllerBase
    {
        [HttpGet("overview")]
        [Authorize(Policy = Policies.Staff)]
        public async Task<ActionResult<SystemOverview>> GetOverview() =>
            Ok(await statisticsService.GetSystemOverviewAsync());

        [HttpGet("audit-logs")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<PagedResponse<AuditLogResponse>>> GetAuditLogs([FromQuery] AuditLogQuery query)
        {
            var (fromUtc, toUtc) = ToUtcRange(businessCalendar, query.From, query.To);
            var filter = new AuditLogFilter
            {
                UserId = query.UserId,
                OnlyFailures = query.OnlyFailures,
                Search = query.Search,
                FromUtc = fromUtc,
                ToUtc = toUtc
            };

            var result = await auditLogService.GetPagedAsync(filter, new PageRequest(query.Page, query.PageSize));
            return Ok(result.ToResponse(log => log.ToResponse()));
        }

        [HttpGet("ledger/trial-balance")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<IEnumerable<TrialBalanceLine>>> GetTrialBalance() =>
            Ok(await ledgerQueryService.GetTrialBalanceAsync());

        [HttpGet("ledger/accounts")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<IEnumerable<LedgerAccountBalance>>> GetLedgerAccounts([FromQuery] bool systemOnly = true) =>
            Ok(await ledgerQueryService.GetLedgerAccountBalancesAsync(systemOnly));

        [HttpGet("ledger/reconciliation")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<IEnumerable<ReconciliationLine>>> GetReconciliation([FromQuery] bool onlyDifferences = false) =>
            Ok(await ledgerQueryService.GetReconciliationAsync(onlyDifferences));

        [HttpPost("legal-documents")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<LegalDocumentResponse>> PublishLegalDocument(PublishLegalDocumentRequest request)
        {
            var document = await legalService.PublishAsync(
                new PublishLegalDocumentCommand(request.DocumentType, request.Version, request.Title, request.Content), CurrentActor);
            return StatusCode(StatusCodes.Status201Created, document.ToResponse());
        }
    }

    [AllowAnonymous]
    public class LegalController(ILegalService legalService) : ApiControllerBase
    {
        [HttpGet("documents")]
        public async Task<ActionResult<IEnumerable<LegalDocumentResponse>>> GetCurrentDocuments() =>
            Ok((await legalService.GetCurrentDocumentsAsync()).Select(d => d.ToResponse()));
    }

    [AllowAnonymous]
    [Route(RoutePrefix + "/reference-data")]
    public class ReferenceDataController(IOptions<BankingSettings> bankingSettings) : ApiControllerBase
    {
        [HttpGet]
        public ActionResult<ReferenceDataResponse> Get() =>
            Ok(new ReferenceDataResponse(
                PaymentCodes.Common.Select(c => new PaymentCodeResponse(c.Key, c.Value)).ToList(),
                PaymentReference.SupportedModels,
                bankingSettings.Value.SupportedCurrencies,
                bankingSettings.Value.BankCode));
    }
}
