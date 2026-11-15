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
    public class CustomerProtectionIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;

        public CustomerProtectionIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private async Task<(User Owner, Account Account)> CreateCustomerAsync(decimal deposit, UserRole role = UserRole.Korisnik, RiskLevel risk = RiskLevel.Nizak)
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
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lozinka123", 4),
                    Role = role,
                    KycStatus = KycStatus.Verifikovan,
                    RiskRating = risk
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

        private static CreatePaymentOrderCommand Payment(Account from, string to, decimal amount, bool strong = false) =>
            new(from.Id, "Primalac", to, null, amount, "289", "Test", null, null, null, null, null, strong);

        private async Task<decimal> FeeIncomeAsync()
        {
            await using var context = _database.CreateContext();
            return await context.Database
                .SqlQueryRaw<decimal>("SELECT COALESCE(SUM(Balance), 0) AS Value FROM v_ledger_account_balances WHERE Code = 'SYS-PROVIZIJE-RSD'")
                .SingleAsync();
        }

        [MySqlFact]
        public async Task Quote_ShowsFeeTotalAndWarnings_AndExecutionChargesExactlyTheQuote()
        {
            var (owner, account) = await CreateCustomerAsync(10_000m);
            var external = SerbianAccountNumber.Create("160", 555000111).Formatted;
            var actor = new Actor(owner.Id, UserRole.Korisnik);
            var feesBefore = await FeeIncomeAsync();

            await using var context = _database.CreateContext();
            var services = new IntegrationServices(context);
            var quote = await services.PaymentOrders.QuoteAsync(Payment(account, external, 100_000m / 10), actor);

            Assert.Equal(10_000m, quote.Amount);
            Assert.Equal(40m, quote.Fee);
            Assert.Equal(10_040m, quote.TotalDebit);
            Assert.True(quote.BalanceAfter < 0);
            Assert.Contains(quote.Warnings, w => w.Contains("Prvi put"));
            Assert.Contains(quote.Warnings, w => w.Contains("nema dovoljno"));

            var affordable = await services.PaymentOrders.QuoteAsync(Payment(account, external, 5_000m), actor);
            var order = (await new IntegrationServices(_database.CreateContext()).PaymentOrders.CreateAsync(Payment(account, external, 5_000m), actor)).Order;

            Assert.Equal(affordable.Fee, order.FeeAmount);
            await using var verify = _database.CreateContext();
            Assert.Equal(10_000m - affordable.TotalDebit, (await verify.Accounts.SingleAsync(a => a.Id == account.Id)).Balance);
            Assert.Equal(feesBefore + affordable.Fee, await FeeIncomeAsync());
        }

        [MySqlFact]
        public async Task LargePaymentToNewRecipient_RequiresStrongConfirmation()
        {
            var (owner, account) = await CreateCustomerAsync(500_000m);
            var (_, recipient) = await CreateCustomerAsync(0m);
            var protection = new CustomerProtectionSettings { StrongConfirmationThresholds = new() { ["RSD"] = 100_000m } };
            var actor = new Actor(owner.Id, UserRole.Korisnik);

            await using var context = _database.CreateContext();
            var service = new IntegrationServices(context, protection: protection).PaymentOrders;

            await Assert.ThrowsAsync<ReauthenticationRequiredException>(() =>
                service.CreateAsync(Payment(account, recipient.AccountNumber, 150_000m), actor));

            var confirmed = await service.CreateAsync(Payment(account, recipient.AccountNumber, 150_000m, strong: true), actor);
            Assert.Equal(PaymentOrderStatus.Izvrsen, confirmed.Order.Status);

            var knownRecipientAgain = await new IntegrationServices(_database.CreateContext(), protection: protection).PaymentOrders
                .CreateAsync(Payment(account, recipient.AccountNumber, 150_000m), actor);
            Assert.Equal(PaymentOrderStatus.Izvrsen, knownRecipientAgain.Order.Status);
        }

        [MySqlFact]
        public async Task ComplianceMonitor_FlagsCashThresholdStructuringAndRapidPayments()
        {
            var compliance = new ComplianceSettings
            {
                CashReportingThresholds = new() { ["RSD"] = 1_000_000m },
                LargePaymentThresholds = new() { ["RSD"] = 50_000m },
                RapidPaymentCount = 3,
                StructuringMinimumOperations = 3
            };
            var (owner, account) = await CreateCustomerAsync(0m);
            var (_, recipient) = await CreateCustomerAsync(0m);
            var banker = new Actor(1, UserRole.Bankar);

            foreach (var amount in new[] { 1_200_000m, 400_000m, 400_000m, 300_000m })
            {
                await using var context = _database.CreateContext();
                await new IntegrationServices(context, compliance: compliance).Payments
                    .DepositAsync(new DepositCommand(account.Id, amount, null, null), banker);
            }

            for (var i = 0; i < 6; i++)
            {
                await using var context = _database.CreateContext();
                await new IntegrationServices(context, compliance: compliance).PaymentOrders
                    .CreateAsync(Payment(account, recipient.AccountNumber, 60_000m), new Actor(owner.Id, UserRole.Korisnik));
            }

            await using var verify = _database.CreateContext();
            var rules = await verify.ComplianceAlerts.Where(a => a.UserId == owner.Id).Select(a => a.RuleCode).ToListAsync();

            Assert.Contains("GOTOVINA_PREKO_PRAGA", rules);
            Assert.Contains("STRUKTURIRANJE_GOTOVINE", rules);
            Assert.Contains("NOVI_PRIMALAC_VELIKI_IZNOS", rules);
            Assert.Contains("BRZE_UZASTOPNE_ISPLATE", rules);
            Assert.Equal(1, rules.Count(r => r == "NOVI_PRIMALAC_VELIKI_IZNOS"));
            Assert.Equal(1, rules.Count(r => r == "BRZE_UZASTOPNE_ISPLATE"));
        }

        [MySqlFact]
        public async Task ComplaintLifecycle_EnforcesTransitionsDeadlinesAndHidesInternalNotes()
        {
            var (customer, account) = await CreateCustomerAsync(100m);
            var (staff, _) = await CreateCustomerAsync(0m, UserRole.Bankar);
            var staffActor = new Actor(staff.Id, UserRole.Bankar);

            await using var context = _database.CreateContext();
            var complaints = new IntegrationServices(context, protection: new CustomerProtectionSettings { ComplaintResponseDays = 15, ComplaintExtensionDays = 15 }).Complaints;

            var complaint = await complaints.SubmitAsync(customer.Id, new SubmitComplaintCommand(
                ComplaintCategory.Naknade, "Naplaćena naknada", "Naplaćena mi je naknada koju nisam očekivao na plaćanju.", null, account.Id));
            var originalDue = complaint.DueAt;
            Assert.StartsWith("PRG-", complaint.ReferenceNumber);
            Assert.InRange((originalDue - complaint.CreatedAt).TotalDays, 14.9, 15.1);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                complaints.GetPagedAsync(new ComplaintFilter(), new PageRequest(), new Actor(customer.Id, UserRole.Korisnik)));

            await complaints.AssignAsync(complaint.Id, staff.Id, staffActor);
            await complaints.AddStaffMessageAsync(complaint.Id, "Interna provera kod kartica.", true, staffActor);
            await complaints.ChangeStatusAsync(complaint.Id, ComplaintStatus.CekaKorisnika, "Molimo dostavite datum.", staffActor);
            await complaints.AddCustomerMessageAsync(customer.Id, complaint.Id, "Datum je 10.11.");

            var extended = await complaints.ExtendDeadlineAsync(complaint.Id, "Potrebna provera sa platnim sistemom.", staffActor);
            Assert.Equal(originalDue.AddDays(15), extended.DueAt);
            await Assert.ThrowsAsync<BusinessRuleException>(() => complaints.ExtendDeadlineAsync(complaint.Id, "Opet", staffActor));

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                complaints.ChangeStatusAsync(complaint.Id, ComplaintStatus.Resen, null, staffActor));
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                complaints.ChangeStatusAsync(complaint.Id, ComplaintStatus.Zatvoren, "x", staffActor));

            await complaints.ChangeStatusAsync(complaint.Id, ComplaintStatus.Resen, "Naknada je vraćena na vaš račun.", staffActor);

            var customerView = await complaints.GetMineAsync(customer.Id, complaint.Id);
            Assert.Equal(ComplaintStatus.Resen, customerView.Status);
            Assert.DoesNotContain(customerView.Messages, m => m.IsInternalNote);
            Assert.Contains(customerView.Messages, m => m.Message.Contains("Narodnoj banci Srbije"));
            Assert.True(customerView.StatusHistory.Count >= 5);

            await using var verify = _database.CreateContext();
            Assert.True(await verify.Notifications.CountAsync(n => n.UserId == customer.Id && n.Type == NotificationType.Prigovor) >= 4);
        }

        [MySqlFact]
        public async Task Feedback_IsUniquePerContext_AndSummaryComputesNps()
        {
            var (first, _) = await CreateCustomerAsync(0m);
            var (second, _) = await CreateCustomerAsync(0m);
            var (third, _) = await CreateCustomerAsync(0m);

            await using var context = _database.CreateContext();
            var feedback = new IntegrationServices(context).Feedback;
            var context1 = $"nps-test-{Guid.NewGuid():N}"[..30];

            await feedback.SubmitAsync(first.Id, new SubmitFeedbackCommand(SurveyType.NPS, 10, "Odlično", context1));
            await feedback.SubmitAsync(second.Id, new SubmitFeedbackCommand(SurveyType.NPS, 9, null, context1));
            await feedback.SubmitAsync(third.Id, new SubmitFeedbackCommand(SurveyType.NPS, 3, "Sporo", context1));

            await Assert.ThrowsAsync<ConflictException>(() =>
                feedback.SubmitAsync(first.Id, new SubmitFeedbackCommand(SurveyType.NPS, 8, null, context1)));
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                feedback.SubmitAsync(first.Id, new SubmitFeedbackCommand(SurveyType.CSAT, 9, null, "csat-test")));

            var summary = await feedback.GetSummaryAsync(DateTime.UtcNow.AddMinutes(-5));
            var nps = summary.Scores.Single(s => s.SurveyType == SurveyType.NPS);
            Assert.True(nps.Responses >= 3);
            Assert.NotNull(nps.Score);
        }

        [MySqlFact]
        public async Task NotificationPreferences_SuppressOptionalButNeverSecurityNotifications()
        {
            var (owner, account) = await CreateCustomerAsync(0m);

            await using (var context = _database.CreateContext())
            {
                await new IntegrationServices(context).Preferences.UpdateAsync(owner.Id,
                    new UpdatePreferencesCommand(false, true, true, true, MarketingConsent: true, false, null));
            }

            await using (var context = _database.CreateContext())
            {
                await new IntegrationServices(context).Payments
                    .DepositAsync(new DepositCommand(account.Id, 1000m, null, null), new Actor(1, UserRole.Bankar));
                await new IntegrationServices(context).Publisher.PublishAsync(new Notification
                {
                    UserId = owner.Id, Type = NotificationType.Bezbednost, Title = "Test", Message = "Bezbednost", CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            await using var verify = _database.CreateContext();
            var types = await verify.Notifications.Where(n => n.UserId == owner.Id).Select(n => n.Type).ToListAsync();
            Assert.DoesNotContain(NotificationType.Uplata, types);
            Assert.Contains(NotificationType.Bezbednost, types);

            var consentHistory = await verify.ConsentEvents.Where(c => c.UserId == owner.Id).ToListAsync();
            Assert.Single(consentHistory);
            Assert.True(consentHistory[0].Granted);
        }

        [MySqlFact]
        public async Task Sessions_CanBeRevoked_AndNewDeviceTriggersSecurityNotification()
        {
            var (owner, _) = await CreateCustomerAsync(0m);
            var expiry = DateTime.UtcNow.AddMinutes(30);

            Guid laptop;
            await using (var context = _database.CreateContext())
                laptop = (await new IntegrationServices(context).Sessions.StartAsync(owner.Id, expiry, Guid.NewGuid().ToString("N"))).Id;

            Guid phone;
            await using (var context = _database.CreateContext())
            {
                var phoneContext = new TestRequestContext { IpAddress = "10.0.0.9", UserAgent = "Mozilla/5.0 (iPhone) Safari/604.1" };
                phone = (await new IntegrationServices(context, requestContext: phoneContext).Sessions.StartAsync(owner.Id, expiry, Guid.NewGuid().ToString("N"))).Id;
            }

            await using (var context = _database.CreateContext())
            {
                var sessions = new IntegrationServices(context).Sessions;
                Assert.Equal(2, (await sessions.GetActiveAsync(owner.Id)).Count);
                Assert.Equal(1, await sessions.RevokeOthersAsync(owner.Id, laptop, "Test"));
                Assert.False(await sessions.ValidateAndTouchAsync(phone, owner.Id));
                Assert.True(await sessions.ValidateAndTouchAsync(laptop, owner.Id));

                await Assert.ThrowsAsync<BusinessRuleException>(() => sessions.ReauthenticateAsync(owner.Id, laptop, "pogresna"));
                Assert.False(await sessions.IsRecentlyReauthenticatedAsync(laptop));
                await sessions.ReauthenticateAsync(owner.Id, laptop, "Lozinka123");
                Assert.True(await sessions.IsRecentlyReauthenticatedAsync(laptop));
            }

            await using var verify = _database.CreateContext();
            Assert.True(await verify.Notifications.AnyAsync(n => n.UserId == owner.Id && n.Title == "Prijava sa novog uređaja"));
        }

        [MySqlFact]
        public async Task DataAccessLog_RecordsStaffAccessOnly()
        {
            var (customer, account) = await CreateCustomerAsync(0m);
            var (staff, _) = await CreateCustomerAsync(0m, UserRole.Bankar);

            await using (var context = _database.CreateContext())
            {
                var logger = new IntegrationServices(context).DataAccess;
                await logger.LogAsync(new Actor(staff.Id, UserRole.Bankar), customer.Id, "Racun", account.Id, "Pregled računa");
                await logger.LogAsync(new Actor(customer.Id, UserRole.Korisnik), customer.Id, "Racun", account.Id, "Sopstveni pregled");
            }

            await using var verify = _database.CreateContext();
            var entries = await new IntegrationServices(verify).DataAccess.GetForSubjectAsync(customer.Id, new PageRequest());
            Assert.Single(entries.Items);
            Assert.Equal(staff.Id, entries.Items[0].ViewerUserId);
        }
    }
}
