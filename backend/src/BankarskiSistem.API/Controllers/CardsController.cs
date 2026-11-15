using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankarskiSistem.API.Controllers
{
    [Route(RoutePrefix)]
    [Authorize]
    public class CardsController(ICardService cardService, IBudgetService budgetService, IBusinessCalendar businessCalendar, IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet("me/cards")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<IEnumerable<CardResponse>>> GetMine() =>
            Ok((await cardService.GetMineAsync(CurrentActor)).Select(Map));

        [HttpGet("users/{userId:int}/cards")]
        [Authorize(Policy = Policies.Staff)]
        public async Task<ActionResult<IEnumerable<CardResponse>>> GetForUser(int userId) =>
            Ok((await cardService.GetForUserAsync(userId, CurrentActor)).Select(Map));

        [HttpPost("accounts/{accountId:int}/cards")]
        [Authorize(Policy = Policies.Staff)]
        public async Task<ActionResult<CardResponse>> Issue(int accountId, IssueCardRequest request)
        {
            var actor = CurrentActor;
            var card = await cardService.IssueAsync(accountId, request.Brand, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Izdata kartica", "Card", card.Id, $"{card.Brand} ...{card.Last4}, racun {accountId}"));
            return StatusCode(StatusCodes.Status201Created, Map(card));
        }

        [HttpPut("cards/{id:int}/settings")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<CardResponse>> UpdateSettings(int id, CardSettingsRequest request)
        {
            var actor = CurrentActor;
            var card = await cardService.UpdateSettingsAsync(actor, id, new CardSettings(
                request.OnlineEnabled, request.ContactlessEnabled, request.AbroadEnabled, request.DailyPurchaseLimit, request.DailyAtmLimit));
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Izmenjena podesavanja kartice", "Card", id,
                $"internet {request.OnlineEnabled}, beskontaktno {request.ContactlessEnabled}, inostranstvo {request.AbroadEnabled}, limiti {request.DailyPurchaseLimit}/{request.DailyAtmLimit}"));
            return Ok(Map(card));
        }

        [HttpPost("cards/{id:int}/block")]
        public async Task<ActionResult<CardResponse>> Block(int id, BlockCardRequest request)
        {
            var actor = CurrentActor;
            var card = await cardService.BlockAsync(actor, id, request.Reason);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Blokirana kartica", "Card", id, card.StatusReason));
            return Ok(Map(card));
        }

        [HttpPost("cards/{id:int}/unblock")]
        public async Task<ActionResult<CardResponse>> Unblock(int id)
        {
            var actor = CurrentActor;
            var card = await cardService.UnblockAsync(actor, id);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Odblokirana kartica", "Card", id));
            return Ok(Map(card));
        }

        [HttpPost("cards/{id:int}/report-lost")]
        [Authorize(Policy = Policies.Customer)]
        [RequireRecentAuthentication]
        public async Task<ActionResult<CardResponse?>> ReportLost(int id, ReportLostCardRequest request)
        {
            var actor = CurrentActor;
            var replacement = await cardService.ReportLostAsync(actor, id, request.Reissue);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Prijavljen gubitak kartice", "Card", id,
                replacement != null ? $"zamenska kartica {replacement.Id}" : "bez zamene"));
            return replacement == null ? NoContent() : Ok(Map(replacement));
        }

        [HttpPost("cards/{id:int}/simulate")]
        [Authorize(Policy = Policies.Customer)]
        [EnableRateLimiting(RateLimitPolicies.Payments)]
        public async Task<ActionResult<CardAuthorizationResponse>> Simulate(int id, SimulateCardPaymentRequest request)
        {
            var actor = CurrentActor;
            var authorization = await cardService.AuthorizeAsync(actor, id, new CardAuthorizationRequest(
                request.Channel, request.MerchantName, request.Amount, request.IsAbroad, request.Contactless, request.CategoryCode));
            if (authorization.Status == CardAuthorizationStatus.Odobrena)
                await budgetService.TryEvaluateAlertsAsync(actor.UserId);
            return Ok(authorization.ToResponse());
        }

        [HttpGet("cards/{id:int}/authorizations")]
        public async Task<ActionResult<PagedResponse<CardAuthorizationResponse>>> GetAuthorizations(int id, int page = 1, int pageSize = 20) =>
            Ok((await cardService.GetAuthorizationsAsync(CurrentActor, id, new PageRequest(page, pageSize))).ToResponse(a => a.ToResponse()));

        private CardResponse Map(Card card)
        {
            var max = CardService.MaxLimits(card.Account?.Currency ?? "RSD");
            return card.ToResponse(businessCalendar.Today(), max.Purchase, max.Atm);
        }
    }
}
