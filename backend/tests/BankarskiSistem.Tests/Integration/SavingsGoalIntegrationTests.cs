using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Core.Settings;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class SavingsGoalIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;

        public SavingsGoalIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Belgrade"));

        private async Task<(User Owner, Account Current)> CreateCustomerAsync(decimal deposit, KycStatus kyc = KycStatus.Verifikovan)
        {
            int ownerId;
            int accountId;
            await using (var context = _database.CreateContext())
            {
                var owner = new User
                {
                    FirstName = "Stediša",
                    LastName = "Test",
                    Email = $"{Guid.NewGuid():N}@test.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lozinka123", 4),
                    Role = UserRole.Korisnik,
                    KycStatus = kyc
                };
                var account = new Account { User = owner, AccountNumber = MySqlDatabaseFixture.CreateAccountNumberGenerator().Generate() };
                context.Accounts.Add(account);
                context.LedgerAccounts.Add(LedgerAccount.ForCustomer(account, DateTime.UtcNow));
                await context.SaveChangesAsync();
                ownerId = owner.Id;
                accountId = account.Id;
            }
            if (deposit > 0)
                await new IntegrationServices(_database.CreateContext()).Payments.DepositAsync(new DepositCommand(accountId, deposit, "Plata", null), new Actor(1, UserRole.Bankar));
            await using var reader = _database.CreateContext();
            return (await reader.Users.SingleAsync(u => u.Id == ownerId), await reader.Accounts.SingleAsync(a => a.Id == accountId));
        }

        private async Task<decimal> BalanceAsync(int accountId)
        {
            await using var context = _database.CreateContext();
            return (await context.Accounts.SingleAsync(a => a.Id == accountId)).Balance;
        }

        [MySqlFact]
        public async Task Goal_OpensSavingsAccount_TracksProgress_AndIsAchieved()
        {
            var (owner, current) = await CreateCustomerAsync(100_000m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);

            var goal = await new IntegrationServices(_database.CreateContext()).Savings.CreateAsync(actor, "Letovanje", 60_000m, Today.AddMonths(6), "RSD");
            Assert.Equal(SavingsGoalStatus.Aktivan, goal.Status);
            Assert.Equal(AccountKind.Stednja, goal.Account!.Kind);

            await new IntegrationServices(_database.CreateContext()).Savings.ContributeAsync(actor, goal.Id, current.Id, 40_000m, "s1");
            var replay = await new IntegrationServices(_database.CreateContext()).Savings.ContributeAsync(actor, goal.Id, current.Id, 40_000m, "s1");
            Assert.Equal(40_000m, replay.Account!.Balance);

            var achieved = await new IntegrationServices(_database.CreateContext()).Savings.ContributeAsync(actor, goal.Id, current.Id, 25_000m, null);
            Assert.Equal(SavingsGoalStatus.Ostvaren, achieved.Status);
            Assert.Equal(35_000m, await BalanceAsync(current.Id));

            var lowered = await new IntegrationServices(_database.CreateContext()).Savings.WithdrawAsync(actor, goal.Id, current.Id, 10_000m, null);
            Assert.Equal(SavingsGoalStatus.Aktivan, lowered.Status);

            await using var verify = _database.CreateContext();
            Assert.True(await verify.Notifications.AnyAsync(n => n.UserId == owner.Id && n.Type == NotificationType.Stednja));
        }

        [MySqlFact]
        public async Task SavingsAccount_BlocksThirdPartyPaymentsAndCash_ButNotOwnTransfersOrDailyLimit()
        {
            var (owner, current) = await CreateCustomerAsync(500_000m);
            var (_, stranger) = await CreateCustomerAsync(0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var goal = await new IntegrationServices(_database.CreateContext()).Savings.CreateAsync(actor, "Stan", 1_000_000m, null, "RSD");

            await new IntegrationServices(_database.CreateContext()).Savings.ContributeAsync(actor, goal.Id, current.Id, 200_000m, null);

            await Assert.ThrowsAsync<BusinessRuleException>(() => new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(
                new CreatePaymentOrderCommand(goal.AccountId, "Neko", stranger.AccountNumber, null, 1_000m, "289", "Test", null, null, null, null, null), actor));
            await Assert.ThrowsAsync<BusinessRuleException>(() => new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(
                new CreatePaymentOrderCommand(goal.AccountId, "Druga banka", SerbianAccountNumber.Create("160", 999000111).Formatted, null, 1_000m, "289", "Test", null, null, null, null, null), actor));
            await Assert.ThrowsAsync<BusinessRuleException>(() => new IntegrationServices(_database.CreateContext()).Payments
                .WithdrawAsync(new WithdrawCommand(goal.AccountId, 1_000m, "Bankomat", null), actor));

            var closed = await new IntegrationServices(_database.CreateContext()).Savings.CloseAsync(actor, goal.Id, current.Id);
            Assert.Equal(SavingsGoalStatus.Zatvoren, closed.Status);
            Assert.Equal(AccountStatus.Neaktivan, closed.Account!.Status);
            Assert.Equal(500_000m, await BalanceAsync(current.Id));
            await Assert.ThrowsAsync<BusinessRuleException>(() => new IntegrationServices(_database.CreateContext()).Savings.ContributeAsync(actor, goal.Id, current.Id, 1m, null));
        }

        [MySqlFact]
        public async Task Goal_RequiresVerifiedIdentity_AndValidInput()
        {
            var (unverified, _) = await CreateCustomerAsync(0m, KycStatus.NijeVerifikovan);
            var (owner, _) = await CreateCustomerAsync(0m);
            var service = new IntegrationServices(_database.CreateContext()).Savings;

            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(new Actor(unverified.Id, UserRole.Korisnik), "Auto", 1_000m, null, "RSD"));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(new Actor(owner.Id, UserRole.Korisnik), "Auto", 1_000m, Today, "RSD"));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(new Actor(owner.Id, UserRole.Korisnik), "Auto", 1_000m, null, "EUR"));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(new Actor(owner.Id, UserRole.Bankar), "Auto", 1_000m, null, "RSD"));
        }

        [MySqlFact]
        public async Task OwnTransfers_IgnoreDailyLimit_WhileThirdPartyPaymentsRespectIt()
        {
            var (owner, current) = await CreateCustomerAsync(500_000m);
            var (_, stranger) = await CreateCustomerAsync(0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var limits = new SecuritySettings { MaxTransactionsPerDay = 100, DefaultDailyLimit = 100_000m, DailyLimitsByCurrency = new() { ["RSD"] = 100_000m } };
            IntegrationServices Limited() => new(_database.CreateContext(), security: limits);

            var goal = await Limited().Savings.CreateAsync(actor, "Kuća", 1_000_000m, null, "RSD");
            var funded = await Limited().Savings.ContributeAsync(actor, goal.Id, current.Id, 250_000m, null);
            Assert.Equal(250_000m, funded.Account!.Balance);

            await Assert.ThrowsAsync<BusinessRuleException>(() => Limited().PaymentOrders.CreateAsync(
                new CreatePaymentOrderCommand(current.Id, "Neko", stranger.AccountNumber, null, 150_000m, "289", "Test", null, null, null, null, null, true), actor));
        }
    }
}
