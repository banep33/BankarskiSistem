using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class ReversalIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;
        private static readonly Actor Banker = new(1, UserRole.Bankar);

        public ReversalIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private async Task<User> CreateUserAsync(UserRole role)
        {
            await using var context = _database.CreateContext();
            var user = new User
            {
                FirstName = "Storno",
                LastName = role.ToString(),
                Email = $"{Guid.NewGuid():N}@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lozinka123", 4),
                Role = role,
                KycStatus = role == UserRole.Korisnik ? KycStatus.Verifikovan : KycStatus.NijePrimenljivo
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private async Task<Account> CreateAccountAsync(User owner, decimal deposit)
        {
            int accountId;
            await using (var context = _database.CreateContext())
            {
                var account = new Account { UserId = owner.Id, AccountNumber = MySqlDatabaseFixture.CreateAccountNumberGenerator().Generate() };
                context.Accounts.Add(account);
                context.LedgerAccounts.Add(LedgerAccount.ForCustomer(account, DateTime.UtcNow));
                await context.SaveChangesAsync();
                accountId = account.Id;
            }
            if (deposit > 0)
            {
                await using var context = _database.CreateContext();
                await new IntegrationServices(context).Payments.DepositAsync(new DepositCommand(accountId, deposit, "Depozit", null), Banker);
            }
            await using var reader = _database.CreateContext();
            return await reader.Accounts.SingleAsync(a => a.Id == accountId);
        }

        private async Task<Transaction> LastTransactionAsync(int accountId)
        {
            await using var context = _database.CreateContext();
            return await context.Transactions.Where(t => t.ToAccountId == accountId || t.FromAccountId == accountId).OrderByDescending(t => t.Id).FirstAsync();
        }

        private async Task<decimal> BalanceAsync(int accountId)
        {
            await using var context = _database.CreateContext();
            return (await context.Accounts.SingleAsync(a => a.Id == accountId)).Balance;
        }

        private async Task AssertReconciledAsync(params int[] accountIds)
        {
            await using var context = _database.CreateContext();
            foreach (var id in accountIds)
            {
                var difference = await context.Database.SqlQuery<decimal>($"SELECT Difference AS Value FROM v_account_reconciliation WHERE AccountId = {id}").SingleAsync();
                Assert.Equal(0m, difference);
            }
        }

        [MySqlFact]
        public async Task ApprovedReversalOfInternalTransfer_RestoresBothBalances_AndLedger()
        {
            var payer = await CreateUserAsync(UserRole.Korisnik);
            var payee = await CreateUserAsync(UserRole.Korisnik);
            var banker = await CreateUserAsync(UserRole.Bankar);
            var admin = await CreateUserAsync(UserRole.Administrator);
            var from = await CreateAccountAsync(payer, 10_000m);
            var to = await CreateAccountAsync(payee, 0m);

            await new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(
                new CreatePaymentOrderCommand(from.Id, "Primalac", to.AccountNumber, null, 2_500m, "289", "Greška", null, null, null, null, null),
                new Actor(payer.Id, UserRole.Korisnik));
            var transfer = await LastTransactionAsync(to.Id);

            var request = await new IntegrationServices(_database.CreateContext()).Reversals
                .RequestAsync(transfer.Id, "Klijent je greškom uplatio pogrešnom primaocu.", new Actor(banker.Id, UserRole.Bankar));
            Assert.Equal(ReversalStatus.Podnet, request.Status);
            await Assert.ThrowsAsync<ConflictException>(() => new IntegrationServices(_database.CreateContext()).Reversals
                .RequestAsync(transfer.Id, "Drugi zahtev za istu transakciju.", new Actor(banker.Id, UserRole.Bankar)));

            var approved = await new IntegrationServices(_database.CreateContext()).Reversals.ApproveAsync(request.Id, "Provereno.", new Actor(admin.Id, UserRole.Administrator));
            Assert.Equal(ReversalStatus.Izvrsen, approved.Status);
            Assert.NotNull(approved.ReversalTransactionId);

            Assert.Equal(10_000m, await BalanceAsync(from.Id));
            Assert.Equal(0m, await BalanceAsync(to.Id));
            await AssertReconciledAsync(from.Id, to.Id);

            await using var verify = _database.CreateContext();
            var storno = await verify.Transactions.SingleAsync(t => t.Id == approved.ReversalTransactionId);
            Assert.Equal(TransactionType.Storno, storno.Type);
            Assert.Equal(to.Id, storno.FromAccountId);
            Assert.Equal(from.Id, storno.ToAccountId);
            Assert.NotNull((await verify.Transactions.SingleAsync(t => t.Id == transfer.Id)).ReversedAt);
            Assert.True(await verify.Notifications.AnyAsync(n => n.UserId == payee.Id && n.Title == "Transakcija je stornirana"));

            await Assert.ThrowsAsync<BusinessRuleException>(() => new IntegrationServices(_database.CreateContext()).Reversals
                .RequestAsync(transfer.Id, "Ponovno storniranje iste transakcije.", new Actor(banker.Id, UserRole.Bankar)));
        }

        [MySqlFact]
        public async Task FourEyes_AndInsufficientFunds_BlockReversal()
        {
            var customer = await CreateUserAsync(UserRole.Korisnik);
            var admin = await CreateUserAsync(UserRole.Administrator);
            var otherAdmin = await CreateUserAsync(UserRole.Administrator);
            var banker = await CreateUserAsync(UserRole.Bankar);
            var account = await CreateAccountAsync(customer, 5_000m);
            var deposit = await LastTransactionAsync(account.Id);

            var own = await new IntegrationServices(_database.CreateContext()).Reversals
                .RequestAsync(deposit.Id, "Uplata proknjižena na pogrešan račun.", new Actor(admin.Id, UserRole.Administrator));
            await Assert.ThrowsAsync<BusinessRuleException>(() => new IntegrationServices(_database.CreateContext()).Reversals
                .ApproveAsync(own.Id, null, new Actor(admin.Id, UserRole.Administrator)));
            await Assert.ThrowsAsync<ForbiddenException>(() => new IntegrationServices(_database.CreateContext()).Reversals
                .ApproveAsync(own.Id, null, new Actor(banker.Id, UserRole.Bankar)));

            await new IntegrationServices(_database.CreateContext()).Payments
                .WithdrawAsync(new WithdrawCommand(account.Id, 4_000m, "Podizanje", null), new Actor(customer.Id, UserRole.Korisnik));

            await Assert.ThrowsAsync<BusinessRuleException>(() => new IntegrationServices(_database.CreateContext()).Reversals
                .ApproveAsync(own.Id, null, new Actor(otherAdmin.Id, UserRole.Administrator)));
            Assert.Equal(1_000m, await BalanceAsync(account.Id));

            var rejected = await new IntegrationServices(_database.CreateContext()).Reversals
                .RejectAsync(own.Id, "Klijent je već podigao novac; rešava se dogovorom.", new Actor(otherAdmin.Id, UserRole.Administrator));
            Assert.Equal(ReversalStatus.Odbijen, rejected.Status);
            await AssertReconciledAsync(account.Id);
        }

        [MySqlFact]
        public async Task ExternalPayment_CannotBeReversed()
        {
            var customer = await CreateUserAsync(UserRole.Korisnik);
            var account = await CreateAccountAsync(customer, 5_000m);
            var external = SerbianAccountNumber.Create("160", 555000222).Formatted;
            await new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(
                new CreatePaymentOrderCommand(account.Id, "Druga banka", external, null, 1_000m, "289", "Test", null, null, null, null, null),
                new Actor(customer.Id, UserRole.Korisnik));
            var payment = await LastTransactionAsync(account.Id);

            Assert.Equal(TransactionType.EksterniTransfer, payment.Type);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => new IntegrationServices(_database.CreateContext()).Reversals
                .RequestAsync(payment.Id, "Klijent želi da povuče plaćanje.", Banker));
            Assert.Contains("kliring", error.Message);
        }
    }
}
