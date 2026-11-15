using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Validators;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Core.Settings;

namespace BankarskiSistem.Tests.Unit
{
    public class ValidatorTests
    {
        [Theory]
        [InlineData(0, false)]
        [InlineData(-5, false)]
        [InlineData(10.555, false)]
        [InlineData(10_000_001, false)]
        [InlineData(0.01, true)]
        [InlineData(1500.50, true)]
        public void MoneyRequest_ValidatesAmount(decimal amount, bool expectedValid)
        {
            var result = new MoneyRequestValidator().Validate(new MoneyRequest(amount, null));
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Theory]
        [InlineData("205-0000001234567-89", true)]
        [InlineData("205-1234567-89", true)]
        [InlineData("205000000123456789", true)]
        [InlineData("205-12345678901234-89", false)]
        [InlineData("20-1234567-89", false)]
        [InlineData("", false)]
        public void CreatePayment_ValidatesAccountNumberFormat(string accountNumber, bool expectedValid)
        {
            var result = new CreatePaymentRequestValidator().Validate(ValidPayment() with { RecipientAccountNumber = accountNumber });
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Theory]
        [InlineData("289", true)]
        [InlineData("189", true)]
        [InlineData("389", false)]
        [InlineData("28", false)]
        public void CreatePayment_ValidatesPaymentCode(string code, bool expectedValid)
        {
            var result = new CreatePaymentRequestValidator().Validate(ValidPayment() with { PaymentCode = code });
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Theory]
        [InlineData(null, null, true)]
        [InlineData("97", "821234", true)]
        [InlineData("97", "83-1234", false)]
        [InlineData("97", null, false)]
        [InlineData(null, "12345", false)]
        [InlineData("11", "123-456", true)]
        [InlineData("55", "123", false)]
        public void CreatePayment_ValidatesReference(string? model, string? reference, bool expectedValid)
        {
            var result = new CreatePaymentRequestValidator().Validate(
                ValidPayment() with { ReferenceModel = model, ReferenceNumber = reference });
            Assert.Equal(expectedValid, result.IsValid);
        }

        private static CreatePaymentRequest ValidPayment() =>
            new(1, "Elektroprivreda Srbije", "205-0000001234567-89", null, 1500m, "289", "Racun za struju", null, null, null, null);

        [Theory]
        [InlineData("Lozinka123", true)]
        [InlineData("kratka1A", true)]
        [InlineData("bezvelikog1", false)]
        [InlineData("BEZMALOG1", false)]
        [InlineData("BezBrojeva", false)]
        [InlineData("Ab1", false)]
        public void CreateUser_EnforcesPasswordPolicy(string password, bool expectedValid)
        {
            var request = new CreateUserRequest("Ana", "Anic", "ana@test.local", password, password, UserRole.Korisnik);
            var result = new CreateUserRequestValidator().Validate(request);
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Fact]
        public void CreateUser_RejectsUndefinedRole()
        {
            var request = new CreateUserRequest("Ana", "Anic", "ana@test.local", "Lozinka123", "Lozinka123", (UserRole)42);
            Assert.False(new CreateUserRequestValidator().Validate(request).IsValid);
        }

        [Theory]
        [InlineData("Ana", true)]
        [InlineData("Đorđe", true)]
        [InlineData("O'Brien-Smith", true)]
        [InlineData("<script>", false)]
        [InlineData("123", false)]
        public void Profile_ValidatesPersonNames(string name, bool expectedValid)
        {
            var result = new UpdateProfileRequestValidator().Validate(new UpdateProfileRequest(name, "Petrovic"));
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Theory]
        [InlineData(0, 0, 1, 20)]
        [InlineData(-3, 500, 1, 20)]
        [InlineData(4, 50, 4, 50)]
        public void PageRequest_ClampsInvalidValues(int page, int pageSize, int expectedPage, int expectedSize)
        {
            var request = new PageRequest(page, pageSize);
            Assert.Equal(expectedPage, request.Page);
            Assert.Equal(expectedSize, request.PageSize);
        }

        [Fact]
        public void SecuritySettings_UsesCurrencySpecificLimitWithFallback()
        {
            var settings = new SecuritySettings
            {
                DefaultDailyLimit = 500,
                DailyLimitsByCurrency = new() { ["EUR"] = 1000 }
            };

            Assert.Equal(1000, settings.GetDailyLimit("EUR"));
            Assert.Equal(500, settings.GetDailyLimit("CHF"));
        }
    }
}
