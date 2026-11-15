using System.Security.Cryptography;
using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Settings;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Security
{
    public class AccountNumberGenerator : IAccountNumberGenerator
    {
        private const long MinAccountSequence = 1_000_000_000L;
        private const long MaxAccountSequence = 9_999_999_999_999L;

        private readonly string _bankCode;

        public AccountNumberGenerator(IOptions<BankingSettings> settings)
        {
            _bankCode = settings.Value.BankCode;
        }

        public string Generate()
        {
            var sequence = MinAccountSequence + (long)(RandomFraction() * (MaxAccountSequence - MinAccountSequence));
            return SerbianAccountNumber.Create(_bankCode, sequence).Formatted;
        }

        private static double RandomFraction()
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            return (BitConverter.ToUInt64(bytes) >> 11) / (double)(1UL << 53);
        }
    }
}
