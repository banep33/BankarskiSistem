using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class CardIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;
        private Actor Banker = null!;
        private Actor Admin = null!;

        public CardIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private IntegrationServices Services() => new(_database.CreateContext());

        private async Task<Actor> CreateStaffAsync(UserRole role)
        {
            await using var context = _database.CreateContext();
            var user = new User
            {
                FirstName = "Kartice",
                LastName = role.ToString(),
                Email = $"{Guid.NewGuid():N}@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lozinka123", 4),
                Role = role,
                KycStatus = KycStatus.NijePrimenljivo
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return new Actor(user.Id, role);
        }

        private async Task EnsureStaffAsync()
        {
            Banker = await CreateStaffAsync(UserRole.Bankar);
            Admin = await CreateStaffAsync(UserRole.Administrator);
        }

        private async Task<(Actor Owner, Account Account)> CreateCustomerAsync(decimal deposit, KycStatus kyc = KycStatus.Verifikovan)
        {
            if (Banker == null)
                await EnsureStaffAsync();
            int ownerId;
            int accountId;
            await using (var context = _database.CreateContext())
            {
                var owner = new User
                {
                    FirstName = "Kartičar",
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
                await Services().Payments.DepositAsync(new DepositCommand(accountId, deposit, "Plata", null), Banker);
            await using var reader = _database.CreateContext();
            return (new Actor(ownerId, UserRole.Korisnik), await reader.Accounts.SingleAsync(a => a.Id == accountId));
        }

        private static CardAuthorizationRequest Purchase(decimal amount, CardChannel channel = CardChannel.Prodavnica, bool abroad = false, bool contactless = false) =>
            new(channel, "Maxi", amount, abroad, contactless, null);

        private async Task<decimal> BalanceAsync(int accountId)
        {
            await using var context = _database.CreateContext();
            return (await context.Accounts.SingleAsync(a => a.Id == accountId)).Balance;
        }

        [Fact]
        public void GeneratedNumbers_PassLuhnCheck_AndUseBrandPrefix()
        {
            foreach (var brand in Enum.GetValues<CardBrand>())
            {
                var pan = CardNumbers.Generate(brand);
                Assert.Equal(16, pan.Length);
                Assert.StartsWith(CardNumbers.Prefix(brand), pan);
                Assert.True(CardNumbers.IsValid(pan));
            }
            Assert.True(CardNumbers.IsValid("4111111111111111"));
            Assert.False(CardNumbers.IsValid("4111111111111112"));
        }

        [MySqlFact]
        public async Task Issue_RequiresStaffKycAndCurrentAccount_AndNeverStoresFullNumber()
        {
            var (owner, account) = await CreateCustomerAsync(0m);
            var (_, unverified) = await CreateCustomerAsync(0m, KycStatus.NijeVerifikovan);

            await Assert.ThrowsAsync<ForbiddenException>(() => Services().Cards.IssueAsync(account.Id, CardBrand.Dina, owner));
            await Assert.ThrowsAsync<BusinessRuleException>(() => Services().Cards.IssueAsync(unverified.Id, CardBrand.Dina, Banker));

            var card = await Services().Cards.IssueAsync(account.Id, CardBrand.Visa, Banker);
            Assert.Equal(CardStatus.Aktivna, card.Status);
            Assert.Equal(64, card.PanHash.Length);
            Assert.Equal(4, card.Last4.Length);
            Assert.Equal(100_000m, card.DailyPurchaseLimit);
            Assert.False(card.AbroadEnabled);

            await Services().Cards.IssueAsync(account.Id, CardBrand.Dina, Banker);
            await Services().Cards.IssueAsync(account.Id, CardBrand.Mastercard, Banker);
            await Assert.ThrowsAsync<BusinessRuleException>(() => Services().Cards.IssueAsync(account.Id, CardBrand.Dina, Banker));
        }

        [MySqlFact]
        public async Task ApprovedPurchase_DebitsAccount_PostsLedger_AndCountsAsSpending()
        {
            var (owner, account) = await CreateCustomerAsync(50_000m);
            var card = await Services().Cards.IssueAsync(account.Id, CardBrand.Dina, Banker);

            var approved = await Services().Cards.AuthorizeAsync(owner, card.Id, new CardAuthorizationRequest(CardChannel.Prodavnica, "Restoran Kod Mike", 3_500m, false, true, "HRANA"));
            Assert.Equal(CardAuthorizationStatus.Odobrena, approved.Status);
            Assert.NotNull(approved.TransactionId);
            Assert.Equal(46_500m, await BalanceAsync(account.Id));

            var atm = await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(10_000m, CardChannel.Bankomat));
            Assert.Equal(CardAuthorizationStatus.Odobrena, atm.Status);

            await using var verify = _database.CreateContext();
            var tx = await verify.Transactions.SingleAsync(t => t.Id == approved.TransactionId);
            Assert.Equal(TransactionType.Kartica, tx.Type);
            var entry = await verify.JournalEntries.Include(e => e.Postings).SingleAsync(e => e.TransactionId == tx.Id);
            Assert.Equal(2, entry.Postings.Count);
            Assert.Equal(0m, entry.Postings.Sum(p => p.Amount));

            var spending = await Services().Budgets.GetSpendingAsync(owner, null);
            Assert.Equal(13_500m, spending.TotalExpense);
            Assert.Contains(spending.Categories, c => c.Code == "HRANA" && c.Amount == 3_500m);
            Assert.Contains(spending.Categories, c => c.Code == "GOTOVINA" && c.Amount == 10_000m);
        }

        [MySqlFact]
        public async Task Controls_LimitsAndFunds_DeclineAndLogWithoutMovingMoney()
        {
            var (owner, account) = await CreateCustomerAsync(20_000m);
            var card = await Services().Cards.IssueAsync(account.Id, CardBrand.Visa, Banker);

            Assert.Contains("inostranstvu", (await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(100m, abroad: true))).DeclineReason);

            await Services().Cards.UpdateSettingsAsync(owner, card.Id, new CardSettings(false, false, true, 5_000m, 1_000m));
            Assert.Contains("internetu", (await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(100m, CardChannel.Internet))).DeclineReason);
            Assert.Contains("Beskontaktno", (await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(100m, contactless: true))).DeclineReason);
            Assert.Equal(CardAuthorizationStatus.Odobrena, (await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(4_000m, abroad: true))).Status);
            Assert.Contains("limit", (await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(1_500m))).DeclineReason);
            Assert.Contains("bankomat", (await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(1_500m, CardChannel.Bankomat))).DeclineReason);

            await Services().Cards.UpdateSettingsAsync(owner, card.Id, new CardSettings(true, true, true, 500_000m, 100_000m));
            var noFunds = await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(50_000m));
            Assert.Equal(CardAuthorizationStatus.Odbijena, noFunds.Status);
            Assert.Null(noFunds.TransactionId);
            await Assert.ThrowsAsync<BusinessRuleException>(() => Services().Cards.UpdateSettingsAsync(owner, card.Id, new CardSettings(true, true, true, 600_000m, 0m)));

            Assert.Equal(16_000m, await BalanceAsync(account.Id));
            var log = await Services().Cards.GetAuthorizationsAsync(owner, card.Id, new PageRequest(1, 50));
            Assert.Equal(7, log.TotalCount);
            Assert.Equal(1, log.Items.Count(a => a.Status == CardAuthorizationStatus.Odobrena));
        }

        [MySqlFact]
        public async Task Block_Unblock_BankBlock_AndLostCardReplacement()
        {
            var (owner, account) = await CreateCustomerAsync(10_000m);
            var (stranger, _) = await CreateCustomerAsync(0m);
            var card = await Services().Cards.IssueAsync(account.Id, CardBrand.Dina, Banker);

            await Assert.ThrowsAsync<NotFoundException>(() => Services().Cards.BlockAsync(stranger, card.Id, null));
            await Services().Cards.BlockAsync(owner, card.Id, null);
            Assert.Equal("Kartica je blokirana.", (await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(100m))).DeclineReason);
            await Services().Cards.UnblockAsync(owner, card.Id);
            Assert.Equal(CardAuthorizationStatus.Odobrena, (await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(100m))).Status);

            await Services().Cards.BlockAsync(Admin, card.Id, "Sumnja na zloupotrebu");
            await Assert.ThrowsAsync<BusinessRuleException>(() => Services().Cards.UnblockAsync(owner, card.Id));
            await Services().Cards.UnblockAsync(Banker, card.Id);

            await Services().Cards.UpdateSettingsAsync(owner, card.Id, new CardSettings(true, true, true, 7_000m, 2_000m));
            var replacement = await Services().Cards.ReportLostAsync(owner, card.Id, true);
            Assert.NotNull(replacement);
            Assert.Equal(card.Id, replacement!.ReplacesCardId);
            Assert.Equal(7_000m, replacement.DailyPurchaseLimit);
            Assert.True(replacement.AbroadEnabled);
            Assert.NotEqual(card.PanHash, replacement.PanHash);

            Assert.Equal("Kartica je ukinuta.", (await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(100m))).DeclineReason);
            await Assert.ThrowsAsync<BusinessRuleException>(() => Services().Cards.UnblockAsync(Banker, card.Id));
            var cards = await Services().Cards.GetMineAsync(owner);
            Assert.Equal(replacement.Id, cards[0].Id);
            Assert.Equal(CardStatus.Ukinuta, cards[1].Status);

            await using var verify = _database.CreateContext();
            Assert.True(await verify.Notifications.CountAsync(n => n.UserId == owner.UserId && n.Type == NotificationType.Kartica) >= 6);
        }

        [MySqlFact]
        public async Task CardPayment_CanBeReversedByFourEyes()
        {
            var (owner, account) = await CreateCustomerAsync(10_000m);
            var card = await Services().Cards.IssueAsync(account.Id, CardBrand.Dina, Banker);
            var approved = await Services().Cards.AuthorizeAsync(owner, card.Id, Purchase(2_000m));

            var request = await Services().Reversals.RequestAsync(approved.TransactionId!.Value, "Dupla naplata kod trgovca", Banker);
            await Services().Reversals.ApproveAsync(request.Id, null, Admin);
            Assert.Equal(10_000m, await BalanceAsync(account.Id));
        }
    }
}
