using System.Security.Cryptography;
using System.Text;
using BankarskiSistem.Core.Enums;

namespace BankarskiSistem.Core.Banking
{
    public static class CardNumbers
    {
        public static string Prefix(CardBrand brand) => brand switch
        {
            CardBrand.Dina => "989100",
            CardBrand.Visa => "411111",
            CardBrand.Mastercard => "550000",
            _ => throw new ArgumentOutOfRangeException(nameof(brand))
        };

        public static string Generate(CardBrand brand)
        {
            var digits = new StringBuilder(Prefix(brand));
            while (digits.Length < 15)
                digits.Append(RandomNumberGenerator.GetInt32(10));
            digits.Append(LuhnCheckDigit(digits.ToString()));
            return digits.ToString();
        }

        public static char LuhnCheckDigit(string partial)
        {
            var sum = 0;
            for (var i = 0; i < partial.Length; i++)
            {
                var digit = partial[partial.Length - 1 - i] - '0';
                if (i % 2 == 0)
                {
                    digit *= 2;
                    if (digit > 9)
                        digit -= 9;
                }
                sum += digit;
            }
            return (char)('0' + (10 - sum % 10) % 10);
        }

        public static bool IsValid(string pan) =>
            pan.Length >= 13 && pan.All(char.IsDigit) && LuhnCheckDigit(pan[..^1]) == pan[^1];

        public static string Hash(string pan) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(pan))).ToLowerInvariant();

        public static string Mask(string last4) => $"•••• •••• •••• {last4}";
    }
}
