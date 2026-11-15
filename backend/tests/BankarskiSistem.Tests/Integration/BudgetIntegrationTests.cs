using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class BudgetIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;

        public BudgetIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private async Task<(User Owner, Account Rsd, Account Second)> CreateCustomerAsync(decimal deposit)
        {
            int ownerId;
            await using (var context = _database.CreateContext())
            {
                var owner = new User
                {
                    FirstName = "Budzet",
                    LastName = "Klijent",
                    Email = $"{Guid.NewGuid():N}@test.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lozinka123", 4),
                    Role = UserRole.Korisnik,
                    KycStatus = KycStatus.Verifikovan
                };
                for (var i = 0; i < 2; i++)
                {
                    var account = new Account { User = owner, AccountNumber = MySqlDatabaseFixture.CreateAccountNumberGenerator().Generate() };
                    context.Accounts.Add(account);
                    context.LedgerAccounts.Add(LedgerAccount.ForCustomer(account, DateTime.UtcNow));
                }
                await context.SaveChangesAsync();
                ownerId = owner.Id;
            }
            await using var reader = _database.CreateContext();
            var accounts = await reader.Accounts.Where(a => a.UserId == ownerId).OrderBy(a => a.Id).ToListAsync();
            if (deposit > 0)
                await new IntegrationServices(_database.CreateContext()).Payments.DepositAsync(new DepositCommand(accounts[0].Id, deposit, "Plata", null), new Actor(1, UserRole.Bankar));
            return (await reader.Users.SingleAsync(u => u.Id == ownerId), accounts[0], accounts[1]);
        }

        private static CreatePaymentOrderCommand Pay(Account from, string to, decimal amount, string code = "289") =>
            new(from.Id, "Primalac", to, null, amount, code, "Test", null, null, null, null, null);

        private async Task<int> CategoryIdAsync(string code)
        {
            await using var context = _database.CreateContext();
            return await context.SpendingCategories.Where(c => c.Code == code).Select(c => c.Id).SingleAsync();
        }

        [MySqlFact]
        public async Task Spending_UsesDefaults_ExcludesOwnTransfersAndReversals_AndAppliesRules()
        {
            var (owner, rsd, second) = await CreateCustomerAsync(50_000m);
            var (_, other, _) = await CreateCustomerAsync(0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var shop = SerbianAccountNumber.Create("160", 888000111).Formatted;

            await new IntegrationServices(_database.CreateContext()).Payments.WithdrawAsync(new WithdrawCommand(rsd.Id, 2_000m, "Bankomat", null), actor);
            await new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(Pay(rsd, shop, 3_000m, "221"), actor);
            await new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(Pay(rsd, other.AccountNumber, 4_000m), actor);
            await new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(Pay(rsd, second.AccountNumber, 10_000m), actor);

            var summary = await new IntegrationServices(_database.CreateContext()).Budgets.GetSpendingAsync(actor, null);
            Assert.Equal(50_000m, summary.TotalIncome);
            Assert.Equal(3, summary.Transactions.Count);
            Assert.Equal(2_000m, summary.Categories.Single(c => c.Code == "GOTOVINA").Amount);
            Assert.True(summary.Categories.Single(c => c.Code == "KUPOVINA").Amount > 3_000m);
            Assert.Equal(4_000m, summary.Categories.Single(c => c.Code == "OSTALO").Amount);

            var transfer = summary.Transactions.Single(t => t.CounterpartyAccountNumber == other.AccountNumber);
            var housing = await CategoryIdAsync("STANOVANJE");
            await new IntegrationServices(_database.CreateContext()).Budgets.AssignCategoryAsync(actor, transfer.TransactionId, housing, rememberForCounterparty: true);
            await new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(Pay(rsd, other.AccountNumber, 1_000m), actor);

            var after = await new IntegrationServices(_database.CreateContext()).Budgets.GetSpendingAsync(actor, null);
            Assert.Equal(5_000m, after.Categories.Single(c => c.Code == "STANOVANJE").Amount);
            Assert.DoesNotContain(after.Categories, c => c.Code == "OSTALO");
            Assert.True(after.Transactions.Single(t => t.TransactionId == transfer.TransactionId).IsExplicit);

            var (stranger, _, _) = await CreateCustomerAsync(0m);
            await Assert.ThrowsAsync<NotFoundException>(() => new IntegrationServices(_database.CreateContext()).Budgets
                .AssignCategoryAsync(new Actor(stranger.Id, UserRole.Korisnik), transfer.TransactionId, housing, false));
        }

        [MySqlFact]
        public async Task BudgetAlerts_FireOncePerThresholdPerMonth()
        {
            var (owner, rsd, _) = await CreateCustomerAsync(20_000m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var cash = await CategoryIdAsync("GOTOVINA");

            await new IntegrationServices(_database.CreateContext()).Budgets.SetBudgetAsync(actor, cash, 5_000m);
            await new IntegrationServices(_database.CreateContext()).Payments.WithdrawAsync(new WithdrawCommand(rsd.Id, 4_200m, "Bankomat", null), actor);
            await new IntegrationServices(_database.CreateContext()).Budgets.EvaluateAlertsAsync(owner.Id);
            await new IntegrationServices(_database.CreateContext()).Budgets.EvaluateAlertsAsync(owner.Id);

            await using (var check = _database.CreateContext())
                Assert.Equal(1, await check.Notifications.CountAsync(n => n.UserId == owner.Id && n.Type == NotificationType.Budzet));

            await new IntegrationServices(_database.CreateContext()).Payments.WithdrawAsync(new WithdrawCommand(rsd.Id, 1_000m, "Bankomat", null), actor);
            await new IntegrationServices(_database.CreateContext()).Budgets.EvaluateAlertsAsync(owner.Id);

            await using var verify = _database.CreateContext();
            var alerts = await verify.Notifications.Where(n => n.UserId == owner.Id && n.Type == NotificationType.Budzet).OrderBy(n => n.Id).ToListAsync();
            Assert.Equal(2, alerts.Count);
            Assert.StartsWith("Prekoračen budžet", alerts[1].Title);
            Assert.Equal(2, await verify.BudgetAlerts.CountAsync(a => verify.Budgets.Any(b => b.Id == a.BudgetId && b.UserId == owner.Id)));

            var budgets = await new IntegrationServices(_database.CreateContext()).Budgets.GetBudgetsAsync(actor);
            await new IntegrationServices(_database.CreateContext()).Budgets.DeleteBudgetAsync(actor, budgets.Single().Id);
            Assert.Empty(await new IntegrationServices(_database.CreateContext()).Budgets.GetBudgetsAsync(actor));
        }
    }
}
