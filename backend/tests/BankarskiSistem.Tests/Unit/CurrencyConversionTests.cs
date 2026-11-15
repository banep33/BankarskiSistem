using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Tests.Unit
{
    public class CurrencyConversionTests
    {
        private static readonly ExchangeRate Eur = new() { Currency = "EUR", BuyRate = 116.5m, MiddleRate = 117.2m, SellRate = 117.9m };

        [Fact]
        public void BuyingEuro_UsesSellRate_AndNeverGivesMoreThanPaidFor()
        {
            var result = CurrencyConversion.Calculate("RSD", "EUR", 10_000m, amountIsTarget: false, Eur);

            Assert.Equal(ExchangeRateType.Prodajni, result.RateType);
            Assert.Equal(10_000m, result.FromAmount);
            Assert.Equal(84.81m, result.ToAmount);
            Assert.True(result.ToAmount * Eur.SellRate <= result.FromAmount);
            Assert.Equal(10_000m - CurrencyConversion.Round(84.81m * 117.2m), result.SpreadRsd);
        }

        [Fact]
        public void BuyingExactEuroAmount_ChargesRoundedUpDinars()
        {
            var result = CurrencyConversion.Calculate("RSD", "EUR", 100m, amountIsTarget: true, Eur);

            Assert.Equal(100m, result.ToAmount);
            Assert.Equal(11_790m, result.FromAmount);
            Assert.Equal(70m, result.SpreadRsd);
        }

        [Fact]
        public void SellingEuro_UsesBuyRate()
        {
            var result = CurrencyConversion.Calculate("EUR", "RSD", 100m, amountIsTarget: false, Eur);

            Assert.Equal(ExchangeRateType.Kupovni, result.RateType);
            Assert.Equal(11_650m, result.ToAmount);
            Assert.Equal(70m, result.SpreadRsd);
        }

        [Theory]
        [InlineData(1_000.00)]
        [InlineData(5_555.55)]
        [InlineData(12_345.67)]
        public void SellingForExactDinars_DeliversAtLeastRequestedAmount(double requested)
        {
            var amount = (decimal)requested;
            var result = CurrencyConversion.Calculate("EUR", "RSD", amount, amountIsTarget: true, Eur);

            Assert.True(result.ToAmount >= amount);
            Assert.True(result.SpreadRsd >= 0);
            Assert.Equal(CurrencyConversion.Floor(result.FromAmount * Eur.BuyRate), result.ToAmount);
        }

        [Fact]
        public void UnsupportedPair_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => CurrencyConversion.Calculate("EUR", "USD", 10m, false, Eur));
        }
    }
}
