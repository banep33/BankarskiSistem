using System.Text;
using BankarskiSistem.Infrastructure.Security;

namespace BankarskiSistem.Tests.Unit
{
    public class TotpTests
    {
        private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

        [Theory]
        [InlineData(59L, "94287082")]
        [InlineData(1111111109L, "07081804")]
        [InlineData(1111111111L, "14050471")]
        [InlineData(1234567890L, "89005924")]
        [InlineData(2000000000L, "69279037")]
        public void ComputeCode_MatchesRfc6238TestVectors(long unixTime, string expected)
        {
            var code = Totp.ComputeCode(RfcSecret, unixTime / Totp.StepSeconds, digits: 8);

            Assert.Equal(expected, code);
        }

        [Fact]
        public void Base32_RoundTrips()
        {
            var secret = Totp.GenerateSecret();

            Assert.Equal(32, secret.Length);
            Assert.Equal(secret, Totp.ToBase32(Totp.FromBase32(secret)));
        }

        [Fact]
        public void FindMatchingStep_AcceptsOneStepDrift_AndRejectsReplay()
        {
            var secret = Totp.GenerateSecret();
            var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
            var step = Totp.StepAt(now);
            var previous = Totp.ComputeCode(Totp.FromBase32(secret), step - 1);
            var current = Totp.ComputeCode(Totp.FromBase32(secret), step);
            var tooOld = Totp.ComputeCode(Totp.FromBase32(secret), step - 3);

            Assert.Equal(step - 1, Totp.FindMatchingStep(secret, previous, now, null));
            Assert.Equal(step, Totp.FindMatchingStep(secret, current[..3] + " " + current[3..], now, null));
            Assert.Null(Totp.FindMatchingStep(secret, tooOld, now, null));
            Assert.Null(Totp.FindMatchingStep(secret, current, now, step));
            Assert.Null(Totp.FindMatchingStep(secret, "12345", now, null));
        }

        [Fact]
        public void RecoveryCodes_AreNormalizedBeforeHashing()
        {
            var code = OpaqueTokens.GenerateRecoveryCode();

            Assert.Matches("^[A-Z2-9]{5}-[A-Z2-9]{5}$", code);
            Assert.Equal(OpaqueTokens.Hash(OpaqueTokens.NormalizeRecoveryCode(code)),
                OpaqueTokens.Hash(OpaqueTokens.NormalizeRecoveryCode(code.ToLowerInvariant().Replace("-", " "))));
        }
    }
}
