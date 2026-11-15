using System.Globalization;
using System.Security.Cryptography;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Exceptions;

namespace BankarskiSistem.Core.Banking
{
    public static class MoneyRules
    {
        public const decimal MaxSingleAmount = 10_000_000m;

        private static readonly CultureInfo SerbianCulture = CultureInfo.GetCultureInfo("sr-Latn-RS");

        public static void EnsureValidAmount(decimal amount)
        {
            if (amount <= 0)
                throw new BusinessRuleException("Iznos mora biti veći od nule.");
            if (amount > MaxSingleAmount)
                throw new BusinessRuleException($"Iznos ne sme biti veći od {MaxSingleAmount.ToString("N0", SerbianCulture)}.");
            if (decimal.Round(amount, 2) != amount)
                throw new BusinessRuleException("Iznos može imati najviše dve decimale.");
        }

        public static void EnsureActive(Account account, string message)
        {
            if (!account.IsActive)
                throw new BusinessRuleException(message);
        }

        public static void EnsureSufficientFunds(Account account, decimal amount)
        {
            if (account.Balance < amount)
                throw new BusinessRuleException("Nedovoljno sredstava na računu.");
        }

        public static string Format(decimal amount, string currency) =>
            $"{amount.ToString("N2", SerbianCulture)} {currency}";

        public static string NewReferenceNumber(DateTime utcNow) =>
            $"TRX-{utcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(5))}";
    }
}
