using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankarskiSistem.API.Controllers
{
    [Route(RoutePrefix)]
    [Authorize(Policy = Policies.Staff)]
    public class ReversalsController(IReversalService reversalService, IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpPost("transactions/{transactionId:int}/reversals")]
        public async Task<ActionResult<TransactionReversalResponse>> Create(int transactionId, CreateReversalRequest request)
        {
            var actor = CurrentActor;
            var reversal = await reversalService.RequestAsync(transactionId, request.Reason, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Zahtev za storniranje", "Transaction", transactionId,
                $"{reversal.ReferenceNumber}: {reversal.Reason}"));
            return StatusCode(StatusCodes.Status201Created, reversal.ToResponse());
        }

        [HttpGet("reversals")]
        public async Task<ActionResult<PagedResponse<TransactionReversalResponse>>> GetAll(
            [FromQuery] ReversalStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
            Ok((await reversalService.GetPagedAsync(status, new PageRequest(page, pageSize), CurrentActor)).ToResponse(r => r.ToResponse()));

        [HttpPost("reversals/{id:int}/approve")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<TransactionReversalResponse>> Approve(int id, ReviewReversalRequest request)
        {
            var actor = CurrentActor;
            var reversal = await reversalService.ApproveAsync(id, request.Note, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Odobreno storniranje", "Transaction", reversal.OriginalTransactionId,
                $"{reversal.ReferenceNumber}, storno transakcija {reversal.ReversalTransactionId}"));
            return Ok(reversal.ToResponse());
        }

        [HttpPost("reversals/{id:int}/reject")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<TransactionReversalResponse>> Reject(int id, ReviewReversalRequest request)
        {
            var actor = CurrentActor;
            var reversal = await reversalService.RejectAsync(id, request.Note ?? string.Empty, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Odbijeno storniranje", "Transaction", reversal.OriginalTransactionId, reversal.ReferenceNumber));
            return Ok(reversal.ToResponse());
        }
    }
}
