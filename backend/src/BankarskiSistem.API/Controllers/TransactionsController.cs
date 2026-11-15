using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankarskiSistem.API.Controllers
{
    public class TransactionsController(
        ITransactionService transactionService,
        IBusinessCalendar businessCalendar,
        IDataAccessLogger dataAccessLogger) : ApiControllerBase
    {
        [HttpGet]
        [Authorize(Policy = Policies.Staff)]
        public async Task<ActionResult<PagedResponse<TransactionResponse>>> GetAll([FromQuery] TransactionQuery query)
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

            var result = await transactionService.GetPagedAsync(filter, new PageRequest(query.Page, query.PageSize), CurrentActor);
            return Ok(result.ToResponse(t => t.ToResponse()));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TransactionResponse>> GetById(int id)
        {
            var actor = CurrentActor;
            var transaction = await transactionService.GetByIdAsync(id, actor);
            var ownerId = transaction.FromAccount?.UserId ?? transaction.ToAccount?.UserId;
            if (ownerId.HasValue)
                await dataAccessLogger.LogAsync(actor, ownerId.Value, "Transakcija", id, "Pregled transakcije");
            return Ok(transaction.ToResponse());
        }

        [HttpGet("by-reference/{referenceNumber}")]
        public async Task<ActionResult<TransactionResponse>> GetByReference(string referenceNumber) =>
            Ok((await transactionService.GetByReferenceAsync(referenceNumber, CurrentActor)).ToResponse());
    }
}
