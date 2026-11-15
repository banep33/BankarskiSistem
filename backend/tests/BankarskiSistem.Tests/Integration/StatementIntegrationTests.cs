using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Services;
using BankarskiSistem.Core.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class StatementIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;

        public StatementIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Belgrade"));

        private async Task<(User Owner, Account Account)> CreateCustomerAsync(decimal deposit)
        {
            int ownerId;
            int accountId;
            await using (var context = _database.CreateContext())
            {
                var owner = new User
                {
                    FirstName = "Izvod",
                    LastName = "Klijent",
                    Email = $"{Guid.NewGuid():N}@test.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lozinka123", 4),
                    Role = UserRole.Korisnik,
                    KycStatus = KycStatus.Verifikovan
                };
                var account = new Account { User = owner, AccountNumber = MySqlDatabaseFixture.CreateAccountNumberGenerator().Generate() };
                context.Accounts.Add(account);
                context.LedgerAccounts.Add(LedgerAccount.ForCustomer(account, DateTime.UtcNow));
                await context.SaveChangesAsync();
                ownerId = owner.Id;
                accountId = account.Id;
            }
            if (deposit > 0)
            {
                await using var context = _database.CreateContext();
                await new IntegrationServices(context).Payments.DepositAsync(new DepositCommand(accountId, deposit, "Plata", null), new Actor(1, UserRole.Bankar));
            }
            await using var reader = _database.CreateContext();
            return (await reader.Users.SingleAsync(u => u.Id == ownerId), await reader.Accounts.SingleAsync(a => a.Id == accountId));
        }

        private StatementService Service() =>
            new(_database.CreateContext(), new BusinessCalendar(TimeProvider.System, Options.Create(new SecuritySettings())), TimeProvider.System);

        [MySqlFact]
        public async Task Statement_MatchesLedger_WithFeesWithdrawalsAndRunningBalance()
        {
            var (owner, account) = await CreateCustomerAsync(20_000m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            await new IntegrationServices(_database.CreateContext()).Payments.WithdrawAsync(new WithdrawCommand(account.Id, 3_000m, "Bankomat", null), actor);
            await new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(
                new CreatePaymentOrderCommand(account.Id, "Struja; \"EPS\"", SerbianAccountNumber.Create("160", 777000111).Formatted, null, 1_000m, "221", "=Račun za struju", null, null, null, null, null),
                actor);

            var statement = await Service().GetAsync(account.Id, Today.AddDays(-1), Today, actor);

            await using var verify = _database.CreateContext();
            var balance = (await verify.Accounts.SingleAsync(a => a.Id == account.Id)).Balance;
            Assert.Equal(0m, statement.OpeningBalance);
            Assert.Equal(balance, statement.ClosingBalance);
            Assert.Equal(20_000m, statement.TotalCredits);
            Assert.Equal(20_000m - balance, statement.TotalDebits);
            Assert.Equal(3, statement.Lines.Count);
            Assert.Equal(statement.OpeningBalance + statement.TotalCredits - statement.TotalDebits, statement.ClosingBalance);
            Assert.Equal(statement.ClosingBalance, statement.Lines[^1].BalanceAfter);
            var external = statement.Lines.Single(l => l.Type == TransactionType.EksterniTransfer);
            Assert.True(external.Debit > 1_000m);
            Assert.Contains("Struja", external.Counterparty);

            var csv = StatementService.ToCsv(statement);
            Assert.StartsWith("﻿", csv);
            Assert.Contains("\"Struja; \"\"EPS\"\"", csv);
            Assert.Contains("20000,00", csv);
            Assert.DoesNotContain(";=", csv);

            var nextMonth = await Service().GetAsync(account.Id, Today, Today, actor);
            Assert.Equal(statement.ClosingBalance, nextMonth.ClosingBalance);
        }

        [MySqlFact]
        public async Task Statement_RespectsOwnershipAndPeriodRules()
        {
            var (owner, account) = await CreateCustomerAsync(1_000m);
            var (stranger, _) = await CreateCustomerAsync(0m);

            await Assert.ThrowsAnyAsync<DomainException>(() => Service().GetAsync(account.Id, Today, Today, new Actor(stranger.Id, UserRole.Korisnik)));
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service().GetAsync(account.Id, Today, Today.AddDays(-1), new Actor(owner.Id, UserRole.Korisnik)));
            await Assert.ThrowsAsync<BusinessRuleException>(() => Service().GetAsync(account.Id, Today.AddDays(-400), Today, new Actor(owner.Id, UserRole.Korisnik)));

            var staffView = await Service().GetAsync(account.Id, Today.AddDays(-30), Today.AddDays(5), new Actor(1, UserRole.Bankar));
            Assert.Equal(Today, staffView.To);
            Assert.Equal(1_000m, staffView.ClosingBalance);
        }

        [Theory]
        [InlineData("=SUM(A1)", "'=SUM(A1)")]
        [InlineData("+381", "'+381")]
        [InlineData("-5", "'-5")]
        [InlineData("@cmd", "'@cmd")]
        [InlineData("običan tekst", "običan tekst")]
        [InlineData("a;b", "\"a;b\"")]
        public void CsvCells_AreEscaped(string input, string expected)
        {
            Assert.Equal(expected, StatementService.EscapeCsv(input));
        }
    }
}
