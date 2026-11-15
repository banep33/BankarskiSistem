using System.Security.Cryptography;
using System.Text;

namespace BankarskiSistem.Infrastructure.Security
{
    public static class Totp
    {
        public const int Digits = 6;
        public const int StepSeconds = 30;
        public const int AllowedDriftSteps = 1;
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static string GenerateSecret() => ToBase32(RandomNumberGenerator.GetBytes(20));

        public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / StepSeconds;

        public static string ComputeCode(byte[] secret, long step, int digits = Digits)
        {
            Span<byte> counter = stackalloc byte[8];
            for (var i = 7; i >= 0; i--)
            {
                counter[i] = (byte)(step & 0xFF);
                step >>= 8;
            }

            using var hmac = new HMACSHA1(secret);
            var hash = hmac.ComputeHash(counter.ToArray());
            var offset = hash[^1] & 0x0F;
            var binary = ((hash[offset] & 0x7F) << 24)
                         | (hash[offset + 1] << 16)
                         | (hash[offset + 2] << 8)
                         | hash[offset + 3];
            var modulo = (int)Math.Pow(10, digits);
            return (binary % modulo).ToString().PadLeft(digits, '0');
        }

        public static long? FindMatchingStep(string base32Secret, string code, DateTimeOffset now, long? lastUsedStep)
        {
            var normalized = new string(code.Where(char.IsDigit).ToArray());
            if (normalized.Length != Digits)
                return null;

            var secret = FromBase32(base32Secret);
            var current = StepAt(now);
            for (var drift = -AllowedDriftSteps; drift <= AllowedDriftSteps; drift++)
            {
                var step = current + drift;
                if (lastUsedStep.HasValue && step <= lastUsedStep.Value)
                    continue;

                var expected = Encoding.ASCII.GetBytes(ComputeCode(secret, step));
                if (CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(normalized)))
                    return step;
            }
            return null;
        }

        public static string BuildOtpAuthUri(string issuer, string accountName, string base32Secret) =>
            $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}" +
            $"?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";

        public static string ToBase32(byte[] data)
        {
            var output = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0, bitsLeft = 0;
            foreach (var value in data)
            {
                buffer = (buffer << 8) | value;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    output.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                    bitsLeft -= 5;
                }
            }
            if (bitsLeft > 0)
                output.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
            return output.ToString();
        }

        public static byte[] FromBase32(string input)
        {
            var clean = input.Trim().TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();
            var output = new List<byte>(clean.Length * 5 / 8);
            int buffer = 0, bitsLeft = 0;
            foreach (var character in clean)
            {
                var index = Base32Alphabet.IndexOf(character);
                if (index < 0)
                    throw new FormatException("Neispravan Base32 zapis.");
                buffer = (buffer << 5) | index;
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                    bitsLeft -= 8;
                }
            }
            return output.ToArray();
        }
    }

    public static class OpaqueTokens
    {
        public static string Generate(int bytes = 32) =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        public static string GenerateRecoveryCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var chars = new char[10];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            return $"{new string(chars, 0, 5)}-{new string(chars, 5, 5)}";
        }

        public static string NormalizeRecoveryCode(string code) =>
            new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
