using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.API.Contracts
{
    public record ExchangeRateResponse(int Id, string Currency, decimal BuyRate, decimal MiddleRate, decimal SellRate, DateTime ValidFrom);

    public record PublishExchangeRatesRequest(IReadOnlyList<ExchangeRateInputRequest> Rates);

    public record ExchangeRateInputRequest(string Currency, decimal BuyRate, decimal MiddleRate, decimal SellRate);

    public record CurrencyExchangeRequest(int FromAccountId, int ToAccountId, decimal Amount, bool AmountIsTarget, int? ExpectedRateId);

    public record CurrencyExchangeResponse(
        int Id,
        string ReferenceNumber,
        int FromAccountId,
        string? FromAccountNumber,
        int ToAccountId,
        string? ToAccountNumber,
        string FromCurrency,
        string ToCurrency,
        decimal FromAmount,
        decimal ToAmount,
        decimal AppliedRate,
        ExchangeRateType RateType,
        decimal MiddleRate,
        DateTime CreatedAt,
        bool IsReplay);
}
