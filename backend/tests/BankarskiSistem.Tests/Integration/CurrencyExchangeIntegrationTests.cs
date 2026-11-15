using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class CurrencyExchangeIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;

        public CurrencyExchangeIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private async Task<(User Owner, Account Rsd, Account Eur, Account Usd)> CreateCustomerAsync(decimal rsd, decimal eur)
        {
            int ownerId;
            await using (var context = _database.CreateContext())
            {
                var owner = new User
                {
                    FirstName = "Devizni",
                    LastName = "Klijent",
                    Email = $"{Guid.NewGuid():N}@test.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lozinka123", 4),
                    Role = UserRole.Korisnik,
                    KycStatus = KycStatus.Verifikovan
                };
                foreach (var currency in new[] { "RSD", "EUR", "USD" })
                {
                    var account = new Account { User = owner, Currency = currency, AccountNumber = MySqlDatabaseFixture.CreateAccountNumberGenerator().Generate() };
                    context.Accounts.Add(account);
                    context.LedgerAccounts.Add(LedgerAccount.ForCustomer(account, DateTime.UtcNow));
                }
                await context.SaveChangesAsync();
                ownerId = owner.Id;
            }

            await using (var context = _database.CreateContext())
            {
                var accounts = await context.Accounts.Where(a => a.UserId == ownerId).ToListAsync();
                var payments = new IntegrationServices(context).Payments;
                if (rsd > 0)
                    await payments.DepositAsync(new DepositCommand(accounts.Single(a => a.Currency == "RSD").Id, rsd, "Depozit", null), new Actor(1, UserRole.Bankar));
                if (eur > 0)
                    await payments.DepositAsync(new DepositCommand(accounts.Single(a => a.Currency == "EUR").Id, eur, "Depozit", null), new Actor(1, UserRole.Bankar));
            }

            await using var reader = _database.CreateContext();
            var all = await reader.Accounts.Where(a => a.UserId == ownerId).ToListAsync();
            return (await reader.Users.SingleAsync(u => u.Id == ownerId), all.Single(a => a.Currency == "RSD"), all.Single(a => a.Currency == "EUR"), all.Single(a => a.Currency == "USD"));
        }

        private async Task AssertLedgerConsistentAsync(params Account[] accounts)
        {
            await using var context = _database.CreateContext();
            var totals = await context.Database.SqlQueryRaw<decimal>("SELECT COALESCE(SUM(Amount), 0) AS Value FROM postings GROUP BY Currency").ToListAsync();
            Assert.All(totals, total => Assert.Equal(0m, total));
            foreach (var account in accounts)
            {
                var difference = await context.Database
                    .SqlQueryRaw<decimal>($"SELECT Difference AS Value FROM v_account_reconciliation WHERE AccountId = {account.Id}")
                    .SingleAsync();
                Assert.Equal(0m, difference);
            }
        }

        private async Task<decimal> FeeIncomeAsync()
        {
            await using var context = _database.CreateContext();
            return await context.Database
                .SqlQueryRaw<decimal>("SELECT COALESCE(SUM(Balance), 0) AS Value FROM v_ledger_account_balances WHERE Code = 'SYS-PROVIZIJE-RSD'")
                .SingleAsync();
        }

        [MySqlFact]
        public async Task BuyAndSellEuro_PostBalancedEntries_AndBookSpreadAsIncome()
        {
            var (owner, rsd, eur, _) = await CreateCustomerAsync(50_000m, 0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var incomeBefore = await FeeIncomeAsync();

            await using var context = _database.CreateContext();
            var service = new IntegrationServices(context).CurrencyExchange;
            var quote = await service.QuoteAsync(new CurrencyExchangeCommand(rsd.Id, eur.Id, 100m, true, null, null), actor);
            var bought = await new IntegrationServices(_database.CreateContext()).CurrencyExchange
                .ExecuteAsync(new CurrencyExchangeCommand(rsd.Id, eur.Id, 100m, true, quote.RateId, "k1"), actor);
            var sold = await new IntegrationServices(_database.CreateContext()).CurrencyExchange
                .ExecuteAsync(new CurrencyExchangeCommand(eur.Id, rsd.Id, 40m, false, null, "k2"), actor);

            Assert.Equal(quote.FromAmount, bought.Exchange.FromAmount);
            Assert.Equal(ExchangeRateType.Prodajni, bought.Exchange.RateType);
            Assert.Equal(ExchangeRateType.Kupovni, sold.Exchange.RateType);

            await using var verify = _database.CreateContext();
            var rsdAfter = await verify.Accounts.SingleAsync(a => a.Id == rsd.Id);
            var eurAfter = await verify.Accounts.SingleAsync(a => a.Id == eur.Id);
            Assert.Equal(50_000m - bought.Exchange.FromAmount + sold.Exchange.ToAmount, rsdAfter.Balance);
            Assert.Equal(60m, eurAfter.Balance);
            Assert.Equal(incomeBefore + bought.Exchange.SpreadRsd + sold.Exchange.SpreadRsd, await FeeIncomeAsync());
            Assert.Equal(4, await verify.Transactions.CountAsync(t => t.Type == TransactionType.Konverzija && t.PerformedByUserId == owner.Id));
            await AssertLedgerConsistentAsync(rsd, eur);
        }

        [MySqlFact]
        public async Task SameIdempotencyKey_ExecutesOnce_AndStaleRateIsRejected()
        {
            var (owner, rsd, eur, _) = await CreateCustomerAsync(20_000m, 0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);

            var first = await new IntegrationServices(_database.CreateContext()).CurrencyExchange
                .ExecuteAsync(new CurrencyExchangeCommand(rsd.Id, eur.Id, 5_000m, false, null, "isti"), actor);
            var replay = await new IntegrationServices(_database.CreateContext()).CurrencyExchange
                .ExecuteAsync(new CurrencyExchangeCommand(rsd.Id, eur.Id, 5_000m, false, null, "isti"), actor);
            Assert.True(replay.IsReplay);
            Assert.Equal(first.Exchange.Id, replay.Exchange.Id);

            await Assert.ThrowsAsync<ConflictException>(() => new IntegrationServices(_database.CreateContext()).CurrencyExchange
                .ExecuteAsync(new CurrencyExchangeCommand(rsd.Id, eur.Id, 1_000m, false, first.Exchange.ExchangeRateId - 1, "drugi"), actor));

            await using var verify = _database.CreateContext();
            Assert.Equal(15_000m, (await verify.Accounts.SingleAsync(a => a.Id == rsd.Id)).Balance);
        }

        [MySqlFact]
        public async Task Rules_RejectInvalidPairsForeignAccountsAndOverdraft_WithoutTouchingDailyLimit()
        {
            var (owner, rsd, eur, usd) = await CreateCustomerAsync(10_000m, 50m);
            var (stranger, strangerRsd, _, _) = await CreateCustomerAsync(10_000m, 0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var service = new IntegrationServices(_database.CreateContext()).CurrencyExchange;

            await Assert.ThrowsAsync<BusinessRuleException>(() => service.QuoteAsync(new CurrencyExchangeCommand(eur.Id, usd.Id, 10m, false, null, null), actor));
            await Assert.ThrowsAsync<NotFoundException>(() => service.QuoteAsync(new CurrencyExchangeCommand(strangerRsd.Id, eur.Id, 10m, false, null, null), actor));
            await Assert.ThrowsAsync<BusinessRuleException>(() => new IntegrationServices(_database.CreateContext()).CurrencyExchange
                .ExecuteAsync(new CurrencyExchangeCommand(eur.Id, rsd.Id, 51m, false, null, null), actor));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.QuoteAsync(new CurrencyExchangeCommand(rsd.Id, eur.Id, 10m, false, null, null), new Actor(stranger.Id, UserRole.Bankar)));

            await new IntegrationServices(_database.CreateContext()).CurrencyExchange
                .ExecuteAsync(new CurrencyExchangeCommand(rsd.Id, eur.Id, 9_000m, false, null, null), actor);
            await using var context = _database.CreateContext();
            var (sentToday, _) = await new BankarskiSistem.Infrastructure.Repositories.TransactionRepository(context)
                .GetOutgoingSinceAsync(rsd.Id, DateTime.UtcNow.AddDays(-1));
            Assert.Equal(0m, sentToday);
        }

        [MySqlFact]
        public async Task PublishingRates_RequiresAdministrator_AndValidSpread()
        {
            var (owner, _, _, _) = await CreateCustomerAsync(0m, 0m);
            var service = new IntegrationServices(_database.CreateContext()).CurrencyExchange;
            var admin = new Actor(1, UserRole.Administrator);

            await Assert.ThrowsAsync<ForbiddenException>(() => service.PublishRatesAsync([new ExchangeRateInput("EUR", 116m, 117m, 118m)], new Actor(owner.Id, UserRole.Korisnik)));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PublishRatesAsync([new ExchangeRateInput("EUR", 118m, 117m, 119m)], admin));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PublishRatesAsync([new ExchangeRateInput("EUR", 50m, 117m, 200m)], admin));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PublishRatesAsync([new ExchangeRateInput("RSD", 1m, 2m, 3m)], admin));

            var published = await service.PublishRatesAsync([new ExchangeRateInput("usd", 107.7m, 108.5m, 109.3m)], admin);
            var current = await new IntegrationServices(_database.CreateContext()).CurrencyExchange.GetCurrentRatesAsync();
            Assert.Equal(published[0].Id, current.Single(r => r.Currency == "USD").Id);
            Assert.Contains(current, r => r.Currency == "EUR");
        }
    }
}
