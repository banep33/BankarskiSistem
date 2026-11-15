using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;

namespace BankarskiSistem.Tests.Unit
{
    public class BankingRulesTests
    {
        [Fact]
        public void AccountNumber_CreatedNumbersHaveValidControlDigits()
        {
            var number = SerbianAccountNumber.Create("205", 1234567);

            Assert.Equal("205-0000001234567-" + number.Control, number.Formatted);
            Assert.True(number.HasValidControlDigits);
        }

        [Fact]
        public void AccountNumber_DetectsWrongControlDigits()
        {
            var valid = SerbianAccountNumber.Create("160", 987654321);
            var wrongControl = ((int.Parse(valid.Control) + 1) % 100).ToString("D2");

            Assert.True(SerbianAccountNumber.TryParse($"160-{valid.AccountPart}-{wrongControl}", out var tampered));
            Assert.False(tampered.HasValidControlDigits);
        }

        [Fact]
        public void AccountNumber_ShortFormIsPaddedToStandardFormat()
        {
            Assert.True(SerbianAccountNumber.TryParse(" 205-7264363-45 ", out var parsed));
            Assert.Equal("205-0000007264363-45", parsed.Formatted);
        }

        [Theory]
        [InlineData("821234", true)]
        [InlineData("82-1234", true)]
        [InlineData("831234", false)]
        [InlineData("1", false)]
        public void Model97_ValidatesControlNumber(string reference, bool expected)
        {
            Assert.Equal(expected, PaymentReference.IsValidModel97(reference));
        }

        [Fact]
        public void JournalEntry_RejectsUnbalancedPostings()
        {
            var entry = JournalEntry.Create("Test", "Test", null, DateTime.UtcNow)
                .Credit(1, 100m, "RSD")
                .Debit(2, 99m, "RSD");

            Assert.Throws<InvalidOperationException>(() => entry.MarkPosted(DateTime.UtcNow));
        }

        [Fact]
        public void JournalEntry_IsBalancedPerCurrency()
        {
            var entry = JournalEntry.Create("Test", "Test", null, DateTime.UtcNow)
                .Credit(1, 100m, "EUR")
                .Debit(2, 100m, "EUR")
                .Credit(3, 11_720m, "RSD")
                .Debit(4, 11_720m, "RSD");

            entry.MarkPosted(DateTime.UtcNow);

            Assert.True(entry.IsPosted);
            Assert.Throws<InvalidOperationException>(() => entry.Credit(5, 1m, "RSD"));
        }
    }
}
