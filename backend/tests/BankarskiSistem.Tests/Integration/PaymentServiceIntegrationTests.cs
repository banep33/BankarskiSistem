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
    public class PaymentServiceIntegrationTests
    {
        private static readonly SecuritySettings GenerousLimits = new() { MaxTransactionsPerDay = 1000, DefaultDailyLimit = 100_000_000 };

        private readonly MySqlDatabaseFixture _database;

        public PaymentServiceIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private async Task<(User Owner, Account Account)> CreateCustomerWithAccountAsync(decimal openingDeposit, string currency = "RSD")
        {
            int ownerId;
            int accountId;
            await using (var context = _database.CreateContext())
            {
                var owner = new User
                {
                    FirstName = "Test",
                    LastName = "Klijent",
                    Email = $"{Guid.NewGuid():N}@test.local",
                    PasswordHash = "x",
                    Role = UserRole.Korisnik
                };
                var account = new Account
                {
                    User = owner,
                    AccountNumber = MySqlDatabaseFixture.CreateAccountNumberGenerator().Generate(),
                    Currency = currency
                };
                context.Accounts.Add(account);
                context.LedgerAccounts.Add(LedgerAccount.ForCustomer(account, DateTime.UtcNow));
                await context.SaveChangesAsync();
                ownerId = owner.Id;
                accountId = account.Id;
            }

            if (openingDeposit > 0)
            {
                await using var context = _database.CreateContext();
                await _database.CreatePaymentService(context, GenerousLimits)
                    .DepositAsync(new DepositCommand(accountId, openingDeposit, "Pocetni depozit", null), new Actor(1, UserRole.Bankar));
            }

            await using var reader = _database.CreateContext();
            return (await reader.Users.SingleAsync(u => u.Id == ownerId), await reader.Accounts.SingleAsync(a => a.Id == accountId));
        }

        private async Task<decimal> GetBalanceAsync(int accountId)
        {
            await using var context = _database.CreateContext();
            return await context.Accounts.Where(a => a.Id == accountId).Select(a => a.Balance).SingleAsync();
        }

        private static CreatePaymentOrderCommand Payment(int fromAccountId, string toAccountNumber, decimal amount,
            DateOnly? scheduledFor = null, string? idempotencyKey = null, string? saveAs = null) =>
            new(fromAccountId, "Primalac Test", toAccountNumber, null, amount, "289", "Test placanje",
                null, null, scheduledFor, saveAs, idempotencyKey);

        private async Task AssertLedgerIsConsistentAsync()
        {
            await using var context = _database.CreateContext();
            var trialBalance = await context.Database
                .SqlQueryRaw<decimal>("SELECT COALESCE(SUM(Total), 0) AS Value FROM v_ledger_trial_balance WHERE Total <> 0")
                .SingleAsync();
            var differences = await context.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM v_account_reconciliation WHERE Difference <> 0")
                .SingleAsync();

            Assert.Equal(0m, trialBalance);
            Assert.Equal(0, differences);

            var ledgerQueries = new BankarskiSistem.Infrastructure.Services.LedgerQueryService(context);
            Assert.All(await ledgerQueries.GetTrialBalanceAsync(), line => Assert.Equal(0m, line.Total));
            Assert.Empty(await ledgerQueries.GetReconciliationAsync(onlyDifferences: true));
            Assert.NotEmpty(await ledgerQueries.GetLedgerAccountBalancesAsync(systemOnly: true));
        }

        [MySqlFact]
        public async Task ConcurrentWithdrawals_NeverOverdrawAccount()
        {
            var (owner, account) = await CreateCustomerWithAccountAsync(1000m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);

            var attempts = Enumerable.Range(0, 20).Select(async _ =>
            {
                await using var context = _database.CreateContext();
                try
                {
                    await _database.CreatePaymentService(context, GenerousLimits)
                        .WithdrawAsync(new WithdrawCommand(account.Id, 300m, null, null), actor);
                    return true;
                }
                catch (BusinessRuleException)
                {
                    return false;
                }
            });

            var outcomes = await Task.WhenAll(attempts);

            Assert.Equal(3, outcomes.Count(success => success));
            Assert.Equal(100m, await GetBalanceAsync(account.Id));
            await AssertLedgerIsConsistentAsync();
        }

        [MySqlFact]
        public async Task ConcurrentOppositePayments_DoNotDeadlockAndPreserveTotal()
        {
            var (ownerA, accountA) = await CreateCustomerWithAccountAsync(5000m);
            var (ownerB, accountB) = await CreateCustomerWithAccountAsync(5000m);

            var payments = Enumerable.Range(0, 20).Select(async index =>
            {
                await using var context = _database.CreateContext();
                var fromA = index % 2 == 0;
                await _database.CreatePaymentOrderService(context, GenerousLimits).CreateAsync(
                    Payment(fromA ? accountA.Id : accountB.Id, fromA ? accountB.AccountNumber : accountA.AccountNumber, 100m),
                    new Actor(fromA ? ownerA.Id : ownerB.Id, UserRole.Korisnik));
            });

            await Task.WhenAll(payments);

            Assert.Equal(5000m, await GetBalanceAsync(accountA.Id));
            Assert.Equal(5000m, await GetBalanceAsync(accountB.Id));
            await AssertLedgerIsConsistentAsync();
        }

        [MySqlFact]
        public async Task InternalPayment_PostsBalancedEntry_AndNotifiesBothParties()
        {
            var (ownerA, accountA) = await CreateCustomerWithAccountAsync(1000m);
            var (ownerB, accountB) = await CreateCustomerWithAccountAsync(50m);

            await using var context = _database.CreateContext();
            var result = await _database.CreatePaymentOrderService(context, GenerousLimits).CreateAsync(
                Payment(accountA.Id, accountB.AccountNumber, 250.75m, saveAs: "Komsija"),
                new Actor(ownerA.Id, UserRole.Korisnik));

            Assert.Equal(PaymentOrderStatus.Izvrsen, result.Order.Status);

            await using var verify = _database.CreateContext();
            var transaction = await verify.Transactions.SingleAsync(t => t.Id == result.Order.TransactionId);
            Assert.Equal(749.25m, transaction.FromBalanceAfter);
            Assert.Equal(300.75m, transaction.ToBalanceAfter);

            var postings = await verify.Postings
                .Where(p => verify.JournalEntries.Any(j => j.Id == p.JournalEntryId && j.TransactionId == transaction.Id && j.IsPosted))
                .ToListAsync();
            Assert.Equal(2, postings.Count);
            Assert.Equal(0m, postings.Sum(p => p.Amount));

            var notified = await verify.Notifications.Where(n => n.TransactionId == transaction.Id).Select(n => n.UserId).ToListAsync();
            Assert.Contains(ownerA.Id, notified);
            Assert.Contains(ownerB.Id, notified);
            Assert.True(await verify.Beneficiaries.AnyAsync(b => b.UserId == ownerA.Id && b.Nickname == "Komsija"));
        }

        [MySqlFact]
        public async Task ExternalPayment_IsDebitedToClearing_AndMarkedInProcessing()
        {
            var (owner, account) = await CreateCustomerWithAccountAsync(2000m);
            var externalAccount = SerbianAccountNumber.Create("160", 123456789).Formatted;

            await using var context = _database.CreateContext();
            var result = await _database.CreatePaymentOrderService(context, GenerousLimits).CreateAsync(
                Payment(account.Id, externalAccount, 500m), new Actor(owner.Id, UserRole.Korisnik));

            Assert.Equal(PaymentOrderStatus.UObradi, result.Order.Status);
            Assert.False(result.Order.IsInternal);
            Assert.Equal(40m, result.Order.FeeAmount);
            Assert.Equal(1460m, await GetBalanceAsync(account.Id));
            await AssertLedgerIsConsistentAsync();
        }

        [MySqlFact]
        public async Task ExternalPayment_WithInvalidControlDigits_IsRejected()
        {
            var (owner, account) = await CreateCustomerWithAccountAsync(2000m);
            var valid = SerbianAccountNumber.Create("160", 123456789);
            var invalid = $"160-{valid.AccountPart}-{(int.Parse(valid.Control) + 1) % 100:D2}";

            await using var context = _database.CreateContext();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                _database.CreatePaymentOrderService(context, GenerousLimits)
                    .CreateAsync(Payment(account.Id, invalid, 100m), new Actor(owner.Id, UserRole.Korisnik)));
            Assert.Equal(2000m, await GetBalanceAsync(account.Id));
        }

        [MySqlFact]
        public async Task ScheduledPayment_IsExecutedByWorker_OrRejectedWhenFundsAreMissing()
        {
            var (owner, account) = await CreateCustomerWithAccountAsync(1000m);
            var (_, recipient) = await CreateCustomerWithAccountAsync(0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

            int affordableId;
            int tooExpensiveId;
            await using (var context = _database.CreateContext())
            {
                var service = _database.CreatePaymentOrderService(context, GenerousLimits);
                affordableId = (await service.CreateAsync(Payment(account.Id, recipient.AccountNumber, 400m, tomorrow), actor)).Order.Id;
                tooExpensiveId = (await service.CreateAsync(Payment(account.Id, recipient.AccountNumber, 5000m, tomorrow), actor)).Order.Id;
            }

            Assert.Equal(1000m, await GetBalanceAsync(account.Id));

            await using (var context = _database.CreateContext())
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE payment_orders SET ScheduledFor = DATE_SUB(CURDATE(), INTERVAL 1 DAY) WHERE Id IN ({affordableId}, {tooExpensiveId})");
            }

            await using (var context = _database.CreateContext())
            {
                await _database.CreatePaymentOrderService(context, GenerousLimits).ExecuteDueOrdersAsync(50, CancellationToken.None);
            }

            await using var verify = _database.CreateContext();
            var orders = await verify.PaymentOrders.Where(p => p.Id == affordableId || p.Id == tooExpensiveId).ToListAsync();
            Assert.Equal(PaymentOrderStatus.Izvrsen, orders.Single(o => o.Id == affordableId).Status);
            var rejected = orders.Single(o => o.Id == tooExpensiveId);
            Assert.Equal(PaymentOrderStatus.Odbijen, rejected.Status);
            Assert.Contains("Nedovoljno", rejected.RejectionReason);
            Assert.Equal(600m, await GetBalanceAsync(account.Id));
            Assert.Equal(400m, await GetBalanceAsync(recipient.Id));
            await AssertLedgerIsConsistentAsync();
        }

        [MySqlFact]
        public async Task ScheduledPayment_CanBeCancelledBeforeExecution()
        {
            var (owner, account) = await CreateCustomerWithAccountAsync(1000m);
            var (_, recipient) = await CreateCustomerWithAccountAsync(0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);

            await using var context = _database.CreateContext();
            var service = _database.CreatePaymentOrderService(context, GenerousLimits);
            var order = (await service.CreateAsync(
                Payment(account.Id, recipient.AccountNumber, 100m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))), actor)).Order;

            var cancelled = await service.CancelAsync(order.Id, actor);

            Assert.Equal(PaymentOrderStatus.Otkazan, cancelled.Status);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CancelAsync(order.Id, actor));
        }

        [MySqlFact]
        public async Task Payment_BetweenDifferentCurrencies_IsRejectedWithoutSideEffects()
        {
            var (owner, rsdAccount) = await CreateCustomerWithAccountAsync(1000m);
            var (_, eurAccount) = await CreateCustomerWithAccountAsync(0m, "EUR");

            await using var context = _database.CreateContext();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                _database.CreatePaymentOrderService(context, GenerousLimits)
                    .CreateAsync(Payment(rsdAccount.Id, eurAccount.AccountNumber, 100m), new Actor(owner.Id, UserRole.Korisnik)));

            Assert.Equal(1000m, await GetBalanceAsync(rsdAccount.Id));
            Assert.Equal(0m, await GetBalanceAsync(eurAccount.Id));
        }

        [MySqlFact]
        public async Task DailyLimit_IsEnforcedPerCurrency()
        {
            var (owner, account) = await CreateCustomerWithAccountAsync(5000m, "EUR");
            var settings = new SecuritySettings { DailyLimitsByCurrency = new() { ["EUR"] = 1000m } };
            var actor = new Actor(owner.Id, UserRole.Korisnik);

            await using var context = _database.CreateContext();
            var service = _database.CreatePaymentService(context, settings);
            await service.WithdrawAsync(new WithdrawCommand(account.Id, 900m, null, null), actor);

            var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.WithdrawAsync(new WithdrawCommand(account.Id, 200m, null, null), actor));
            Assert.Contains("dnevni limit", exception.Message);
            Assert.Equal(4100m, await GetBalanceAsync(account.Id));
        }

        [MySqlFact]
        public async Task ConcurrentPaymentsWithSameIdempotencyKey_ExecuteOnce()
        {
            var (owner, account) = await CreateCustomerWithAccountAsync(1000m);
            var (_, recipient) = await CreateCustomerWithAccountAsync(0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var key = Guid.NewGuid().ToString("N");

            var attempts = Enumerable.Range(0, 5).Select(async _ =>
            {
                await using var context = _database.CreateContext();
                return await _database.CreatePaymentOrderService(context, GenerousLimits)
                    .CreateAsync(Payment(account.Id, recipient.AccountNumber, 100m, idempotencyKey: key), actor);
            });

            var results = await Task.WhenAll(attempts);

            Assert.Single(results.Select(r => r.Order.Id).Distinct());
            Assert.Equal(900m, await GetBalanceAsync(account.Id));
        }

        [MySqlFact]
        public async Task Database_RejectsNegativeBalanceAndLedgerTampering()
        {
            var (owner, account) = await CreateCustomerWithAccountAsync(500m);

            await using var raw = _database.CreateContext();
            await Assert.ThrowsAnyAsync<Exception>(() =>
                raw.Database.ExecuteSqlInterpolatedAsync($"UPDATE accounts SET Balance = -1 WHERE Id = {account.Id}"));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                raw.Database.ExecuteSqlInterpolatedAsync($"UPDATE transactions SET Amount = 1 WHERE ToAccountId = {account.Id}"));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                raw.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM transactions WHERE ToAccountId = {account.Id}"));
            await Assert.ThrowsAnyAsync<Exception>(() =>
                raw.Database.ExecuteSqlRawAsync("UPDATE postings SET Amount = Amount + 1 LIMIT 1"));
            Assert.Equal(owner.Id, account.UserId);
        }
    }
}
