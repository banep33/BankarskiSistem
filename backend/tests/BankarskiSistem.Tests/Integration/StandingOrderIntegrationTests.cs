using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class StandingOrderIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;

        public StandingOrderIntegrationTests(MySqlDatabaseFixture database)
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
                    FirstName = "Trajni",
                    LastName = "Nalog",
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
                await new IntegrationServices(context).Payments
                    .DepositAsync(new DepositCommand(accountId, deposit, "Depozit", null), new Actor(1, UserRole.Bankar));
            }

            await using var reader = _database.CreateContext();
            return (await reader.Users.SingleAsync(u => u.Id == ownerId), await reader.Accounts.SingleAsync(a => a.Id == accountId));
        }

        private static CreateStandingOrderCommand Command(Account from, Account to, decimal amount, DateOnly start, int? max = null) =>
            new(from.Id, "Stanodavac", to.AccountNumber, null, amount, "289", "Stanarina", null, null, StandingOrderFrequency.Mesecno, start, null, max);

        private async Task<StandingOrder> InsertPastOrderAsync(User owner, Account from, Account to, decimal amount, DateOnly start)
        {
            await using var context = _database.CreateContext();
            var order = new StandingOrder
            {
                ReferenceNumber = $"TN-T-{Guid.NewGuid():N}"[..20],
                UserId = owner.Id,
                FromAccountId = from.Id,
                RecipientName = "Stanodavac",
                RecipientAccountNumber = to.AccountNumber,
                Amount = amount,
                Currency = "RSD",
                PaymentCode = "289",
                Purpose = "Stanarina",
                Frequency = StandingOrderFrequency.Mesecno,
                StartDate = start,
                NextExecutionDate = start,
                Status = StandingOrderStatus.Aktivan
            };
            context.StandingOrders.Add(order);
            await context.SaveChangesAsync();
            return order;
        }

        private async Task<int> RunAsync() =>
            await new IntegrationServices(_database.CreateContext()).StandingOrders.ExecuteDueAsync(500, CancellationToken.None);

        [MySqlFact]
        public async Task DueOrder_ExecutesOncePerOccurrence_AndMovesToNextMonth()
        {
            var (owner, from) = await CreateCustomerAsync(10_000m);
            var (_, to) = await CreateCustomerAsync(0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);

            var created = await new IntegrationServices(_database.CreateContext()).StandingOrders.CreateAsync(Command(from, to, 1_500m, Today), actor);
            Assert.Equal(Today, created.NextExecutionDate);

            Assert.True(await RunAsync() >= 1);
            await RunAsync();

            await using var verify = _database.CreateContext();
            var order = await verify.StandingOrders.Include(o => o.Executions).SingleAsync(o => o.Id == created.Id);
            Assert.Equal(1, order.ExecutionsCount);
            Assert.Equal(Today.AddMonths(1), order.NextExecutionDate);
            var execution = Assert.Single(order.Executions);
            Assert.Equal(StandingOrderExecutionStatus.Izvrsen, execution.Status);
            Assert.NotNull(execution.PaymentOrderId);
            Assert.Equal(8_500m, (await verify.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
            Assert.Equal(1_500m, (await verify.Accounts.SingleAsync(a => a.Id == to.Id)).Balance);
        }

        [MySqlFact]
        public async Task RepeatedFailures_AreLoggedNotified_AndPauseTheOrder()
        {
            var (owner, from) = await CreateCustomerAsync(100m);
            var (_, to) = await CreateCustomerAsync(0m);
            var inserted = await InsertPastOrderAsync(owner, from, to, 5_000m, Today.AddMonths(-2));

            await RunAsync();
            await using (var check = _database.CreateContext())
            {
                var afterFirst = await check.StandingOrders.SingleAsync(o => o.Id == inserted.Id);
                Assert.Equal(1, afterFirst.ConsecutiveFailures);
                Assert.Equal(StandingOrderStatus.Aktivan, afterFirst.Status);
                Assert.Equal(Today, afterFirst.NextExecutionDate);
                Assert.Equal(2, afterFirst.OccurrenceIndex);
            }

            await RunAsync();
            await using (var context = _database.CreateContext())
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE standing_orders SET NextExecutionDate = {Today.AddDays(-1)} WHERE Id = {inserted.Id}");
            }
            await RunAsync();

            await using var verify = _database.CreateContext();
            var order = await verify.StandingOrders.Include(o => o.Executions).SingleAsync(o => o.Id == inserted.Id);
            Assert.Equal(StandingOrderStatus.Pauziran, order.Status);
            Assert.Equal(3, order.ConsecutiveFailures);
            Assert.Equal(0, order.ExecutionsCount);
            Assert.Equal(3, order.Executions.Count);
            Assert.All(order.Executions, e => Assert.Equal(StandingOrderExecutionStatus.Odbijen, e.Status));
            Assert.All(order.Executions, e => Assert.False(string.IsNullOrWhiteSpace(e.FailureReason)));
            Assert.Equal(100m, (await verify.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
            Assert.True(await verify.Notifications.AnyAsync(n => n.UserId == owner.Id && n.Title == "Trajni nalog je pauziran"));

            var resumed = await new IntegrationServices(_database.CreateContext()).StandingOrders.ResumeAsync(inserted.Id, new Actor(owner.Id, UserRole.Korisnik));
            Assert.Equal(StandingOrderStatus.Aktivan, resumed.Status);
            Assert.Equal(0, resumed.ConsecutiveFailures);
            Assert.True(resumed.NextExecutionDate >= Today);
        }

        [MySqlFact]
        public async Task MaxExecutions_FinishesOrder_AndSkippedPastDatesAreNotBackfilled()
        {
            var (owner, from) = await CreateCustomerAsync(10_000m);
            var (_, to) = await CreateCustomerAsync(0m);
            var inserted = await InsertPastOrderAsync(owner, from, to, 1_000m, Today.AddMonths(-2));
            await using (var context = _database.CreateContext())
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE standing_orders SET MaxExecutions = 3 WHERE Id = {inserted.Id}");
            }

            await RunAsync();
            await RunAsync();
            await RunAsync();

            await using var verify = _database.CreateContext();
            var order = await verify.StandingOrders.SingleAsync(o => o.Id == inserted.Id);
            Assert.Equal(StandingOrderStatus.Zavrsen, order.Status);
            Assert.Null(order.NextExecutionDate);
            Assert.Equal(2, order.ExecutionsCount);
            Assert.Equal(3, order.OccurrenceIndex);
            Assert.Equal(8_000m, (await verify.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
            Assert.True(await verify.Notifications.AnyAsync(n => n.UserId == owner.Id && n.Title == "Trajni nalog je završen"));
        }

        [MySqlFact]
        public async Task ParallelWorkers_ExecuteTheOccurrenceExactlyOnce()
        {
            var (owner, from) = await CreateCustomerAsync(10_000m);
            var (_, to) = await CreateCustomerAsync(0m);
            var inserted = await InsertPastOrderAsync(owner, from, to, 700m, Today);

            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RunAsync()));

            await using var verify = _database.CreateContext();
            Assert.Equal(1, await verify.StandingOrderExecutions.CountAsync(e => e.StandingOrderId == inserted.Id));
            Assert.Equal(9_300m, (await verify.Accounts.SingleAsync(a => a.Id == from.Id)).Balance);
        }

        [MySqlFact]
        public async Task Create_ValidatesRecipient_AndCancelledOrderNeverRuns()
        {
            var (owner, from) = await CreateCustomerAsync(10_000m);
            var (_, to) = await CreateCustomerAsync(0m);
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var services = new IntegrationServices(_database.CreateContext()).StandingOrders;

            var missing = Command(from, to, 100m, Today) with { RecipientAccountNumber = MySqlDatabaseFixture.CreateAccountNumberGenerator().Generate() };
            await Assert.ThrowsAsync<NotFoundException>(() => services.CreateAsync(missing, actor));
            await Assert.ThrowsAsync<ForbiddenException>(() => services.CreateAsync(Command(from, to, 100m, Today), new Actor(owner.Id, UserRole.Bankar)));

            var created = await new IntegrationServices(_database.CreateContext()).StandingOrders.CreateAsync(Command(from, to, 100m, Today.AddDays(1)), actor);
            var cancelled = await new IntegrationServices(_database.CreateContext()).StandingOrders.CancelAsync(created.Id, actor);
            Assert.Equal(StandingOrderStatus.Otkazan, cancelled.Status);
            Assert.Null(cancelled.NextExecutionDate);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                new IntegrationServices(_database.CreateContext()).StandingOrders.PauseAsync(created.Id, actor));
        }
    }
}
