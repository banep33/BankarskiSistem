using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BankarskiSistem.API.Controllers
{
    [Route(RoutePrefix)]
    public class ExchangeController(ICurrencyExchangeService exchangeService, IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet("exchange-rates")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ExchangeRateResponse>>> GetRates() =>
            Ok((await exchangeService.GetCurrentRatesAsync()).Select(ToResponse));

        [HttpGet("exchange-rates/history")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<ExchangeRateResponse>>> GetHistory([FromQuery] string currency = "EUR", [FromQuery] int days = 30) =>
            Ok((await exchangeService.GetHistoryAsync(currency, days)).Select(ToResponse));

        [HttpPost("admin/exchange-rates")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<ActionResult<IEnumerable<ExchangeRateResponse>>> Publish(PublishExchangeRatesRequest request)
        {
            var actor = CurrentActor;
            var published = await exchangeService.PublishRatesAsync(
                request.Rates.Select(r => new ExchangeRateInput(r.Currency, r.BuyRate, r.MiddleRate, r.SellRate)).ToList(), actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Objavljena kursna lista", "ExchangeRate",
                NewValue: string.Join("; ", published.Select(r => $"{r.Currency} {r.BuyRate}/{r.MiddleRate}/{r.SellRate}"))));
            return Ok(published.Select(ToResponse));
        }

        [HttpPost("exchange/quote")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<ExchangeQuote>> Quote(CurrencyExchangeRequest request) =>
            Ok(await exchangeService.QuoteAsync(ToCommand(request, null), CurrentActor));

        [HttpPost("exchange")]
        [Authorize(Policy = Policies.Customer)]
        [RequireAcceptedTerms]
        [EnableRateLimiting(RateLimitPolicies.Payments)]
        public async Task<ActionResult<CurrencyExchangeResponse>> Execute(
            CurrencyExchangeRequest request, [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey)
        {
            var actor = CurrentActor;
            var result = await exchangeService.ExecuteAsync(ToCommand(request, NormalizeIdempotencyKey(idempotencyKey)), actor);
            if (!result.IsReplay)
            {
                await auditLog.LogAsync(new AuditEntry(actor.UserId, "Konverzija valuta", "CurrencyExchange", result.Exchange.Id,
                    $"{result.Exchange.FromAmount} {result.Exchange.FromCurrency} u {result.Exchange.ToAmount} {result.Exchange.ToCurrency}, kurs {result.Exchange.AppliedRate}"));
            }
            return Ok(ToResponse(result.Exchange, result.IsReplay));
        }

        [HttpGet("exchange")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<PagedResponse<CurrencyExchangeResponse>>> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
            Ok((await exchangeService.GetMineAsync(CurrentActor, new PageRequest(page, pageSize))).ToResponse(x => ToResponse(x, false)));

        private static CurrencyExchangeCommand ToCommand(CurrencyExchangeRequest request, string? idempotencyKey) =>
            new(request.FromAccountId, request.ToAccountId, request.Amount, request.AmountIsTarget, request.ExpectedRateId, idempotencyKey);

        private static ExchangeRateResponse ToResponse(ExchangeRate rate) =>
            new(rate.Id, rate.Currency, rate.BuyRate, rate.MiddleRate, rate.SellRate, rate.ValidFrom);

        private static CurrencyExchangeResponse ToResponse(CurrencyExchange exchange, bool isReplay) =>
            new(exchange.Id, exchange.ReferenceNumber, exchange.FromAccountId, exchange.FromAccount?.AccountNumber, exchange.ToAccountId,
                exchange.ToAccount?.AccountNumber, exchange.FromCurrency, exchange.ToCurrency, exchange.FromAmount, exchange.ToAmount,
                exchange.AppliedRate, exchange.RateType, exchange.MiddleRate, exchange.CreatedAt, isReplay);
    }
}
