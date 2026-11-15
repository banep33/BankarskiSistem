using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Banking
{
    public record ConversionResult(decimal FromAmount, decimal ToAmount, decimal AppliedRate, ExchangeRateType RateType, decimal MiddleRate, decimal SpreadRsd);

    public static class CurrencyConversion
    {
        public const string DomesticCurrency = "RSD";

        public static ConversionResult Calculate(string fromCurrency, string toCurrency, decimal amount, bool amountIsTarget, ExchangeRate rate)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            if (fromCurrency == DomesticCurrency && toCurrency == rate.Currency)
            {
                var foreign = amountIsTarget ? amount : Floor(amount / rate.SellRate);
                var domestic = amountIsTarget ? Ceiling(foreign * rate.SellRate) : amount;
                var spread = domestic - Round(foreign * rate.MiddleRate);
                return new ConversionResult(domestic, foreign, rate.SellRate, ExchangeRateType.Prodajni, rate.MiddleRate, Math.Max(0, spread));
            }

            if (fromCurrency == rate.Currency && toCurrency == DomesticCurrency)
            {
                var foreign = amountIsTarget ? Ceiling(amount / rate.BuyRate) : amount;
                var domestic = Floor(foreign * rate.BuyRate);
                var spread = Round(foreign * rate.MiddleRate) - domestic;
                return new ConversionResult(foreign, domestic, rate.BuyRate, ExchangeRateType.Kupovni, rate.MiddleRate, Math.Max(0, spread));
            }

            throw new ArgumentException("Kurs ne odgovara paru valuta.");
        }

        public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        public static decimal Floor(decimal value) => decimal.Floor(value * 100m) / 100m;

        public static decimal Ceiling(decimal value) => decimal.Ceiling(value * 100m) / 100m;
    }
}
