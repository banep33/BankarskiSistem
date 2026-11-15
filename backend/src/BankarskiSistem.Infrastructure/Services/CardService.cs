using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using BankarskiSistem.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Services
{
    public class CardService : ICardService
    {
        public const int MaxActiveCardsPerAccount = 3;
        public const int ValidityYears = 3;

        private readonly ApplicationDbContext _context;
        private readonly IAccountRepository _accountRepository;
        private readonly ILedgerRepository _ledgerRepository;
        private readonly LedgerRecorder _ledgerRecorder;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IBusinessCalendar _businessCalendar;
        private readonly TimeProvider _timeProvider;

        public CardService(
            ApplicationDbContext context,
            IAccountRepository accountRepository,
            ILedgerRepository ledgerRepository,
            LedgerRecorder ledgerRecorder,
            IUnitOfWork unitOfWork,
            INotificationPublisher notificationPublisher,
            IBusinessCalendar businessCalendar,
            TimeProvider timeProvider)
        {
            _context = context;
            _accountRepository = accountRepository;
            _ledgerRepository = ledgerRepository;
            _ledgerRecorder = ledgerRecorder;
            _unitOfWork = unitOfWork;
            _notificationPublisher = notificationPublisher;
            _businessCalendar = businessCalendar;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<Card> IssueAsync(int accountId, CardBrand brand, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException("Karticu izdaje zaposleni banke.");
            var account = await _context.Accounts.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == accountId)
                ?? throw new NotFoundException("Račun nije pronađen.");
            if (account.Kind != AccountKind.Tekuci || account.Status != AccountStatus.Aktivan)
                throw new BusinessRuleException("Kartica se izdaje samo uz aktivan tekući račun.");
            if (account.User.KycStatus != KycStatus.Verifikovan || !account.User.IsActive)
                throw new BusinessRuleException("Kartica se izdaje samo aktivnom klijentu sa verifikovanim identitetom.");
            if (await _context.Cards.CountAsync(c => c.AccountId == accountId && c.Status != CardStatus.Ukinuta) >= MaxActiveCardsPerAccount)
                throw new BusinessRuleException($"Uz jedan račun može biti najviše {MaxActiveCardsPerAccount} važeće kartice.");

            var card = await CreateCardAsync(account, brand, account.User.FullName.ToUpperInvariant(), actor.UserId, null);
            await _notificationPublisher.PublishAsync(Notify(account.UserId, "Izdata je nova kartica",
                $"Uz račun izdata je {brand} debitna kartica sa poslednjim ciframa {card.Last4}, važi do {card.ExpiryMonth:00}/{card.ExpiryYear % 100:00}."));
            await _context.SaveChangesAsync();
            return await LoadAsync(card.Id);
        }

        public async Task<IReadOnlyList<Card>> GetMineAsync(Actor actor)
        {
            EnsureCustomer(actor);
            return await QueryForUser(actor.UserId).ToListAsync();
        }

        public async Task<IReadOnlyList<Card>> GetForUserAsync(int userId, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException();
            return await QueryForUser(userId).ToListAsync();
        }

        public async Task<Card> UpdateSettingsAsync(Actor actor, int cardId, CardSettings settings)
        {
            var card = await LoadOwnedAsync(cardId, actor);
            if (card.Status == CardStatus.Ukinuta)
                throw new BusinessRuleException("Kartica je ukinuta.");
            var max = CardLimits.Max(card.Account!.Currency);
            if (settings.DailyPurchaseLimit < 0 || settings.DailyPurchaseLimit > max.Purchase)
                throw new BusinessRuleException($"Dnevni limit za plaćanje mora biti između 0 i {MoneyRules.Format(max.Purchase, card.Account.Currency)}.");
            if (settings.DailyAtmLimit < 0 || settings.DailyAtmLimit > max.Atm)
                throw new BusinessRuleException($"Dnevni limit na bankomatu mora biti između 0 i {MoneyRules.Format(max.Atm, card.Account.Currency)}.");

            card.OnlineEnabled = settings.OnlineEnabled;
            card.ContactlessEnabled = settings.ContactlessEnabled;
            card.AbroadEnabled = settings.AbroadEnabled;
            card.DailyPurchaseLimit = settings.DailyPurchaseLimit;
            card.DailyAtmLimit = settings.DailyAtmLimit;
            card.UpdatedAt = UtcNow;
            await _context.SaveChangesAsync();
            return await LoadAsync(cardId);
        }

        public async Task<Card> BlockAsync(Actor actor, int cardId, string? reason)
        {
            var card = actor.IsStaff ? await _context.Cards.Include(c => c.Account).FirstOrDefaultAsync(c => c.Id == cardId) : await LoadOwnedAsync(cardId, actor);
            if (card == null)
                throw new NotFoundException("Kartica nije pronađena.");
            if (card.Status != CardStatus.Aktivna)
                throw new BusinessRuleException("Blokirati se može samo aktivna kartica.");
            card.Status = CardStatus.Blokirana;
            card.BlockedByBank = actor.IsStaff;
            card.StatusReason = string.IsNullOrWhiteSpace(reason) ? (actor.IsStaff ? "Blokirala banka" : "Privremeno blokirao klijent") : reason.Trim();
            card.UpdatedAt = UtcNow;
            await _notificationPublisher.PublishAsync(Notify(card.UserId, "Kartica je blokirana",
                $"Kartica ...{card.Last4} je privremeno blokirana. Plaćanja i podizanje gotovine nisu mogući dok se ne odblokira."));
            await _context.SaveChangesAsync();
            return await LoadAsync(cardId);
        }

        public async Task<Card> UnblockAsync(Actor actor, int cardId)
        {
            var card = actor.IsStaff ? await _context.Cards.Include(c => c.Account).FirstOrDefaultAsync(c => c.Id == cardId) : await LoadOwnedAsync(cardId, actor);
            if (card == null)
                throw new NotFoundException("Kartica nije pronađena.");
            if (card.Status != CardStatus.Blokirana)
                throw new BusinessRuleException("Kartica nije blokirana.");
            if (card.BlockedByBank && !actor.IsStaff)
                throw new BusinessRuleException("Karticu je blokirala banka. Za odblokiranje se obratite banci.");
            card.Status = CardStatus.Aktivna;
            card.BlockedByBank = false;
            card.StatusReason = null;
            card.UpdatedAt = UtcNow;
            await _notificationPublisher.PublishAsync(Notify(card.UserId, "Kartica je ponovo aktivna", $"Kartica ...{card.Last4} je odblokirana."));
            await _context.SaveChangesAsync();
            return await LoadAsync(cardId);
        }

        public async Task<Card?> ReportLostAsync(Actor actor, int cardId, bool reissue)
        {
            var card = await LoadOwnedAsync(cardId, actor);
            if (card.Status == CardStatus.Ukinuta)
                throw new BusinessRuleException("Kartica je već ukinuta.");

            var replacement = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                card.Status = CardStatus.Ukinuta;
                card.StatusReason = "Prijavljen gubitak ili krađa";
                card.CancelledAt = UtcNow;
                card.UpdatedAt = UtcNow;
                Card? created = null;
                if (reissue)
                    created = await CreateCardAsync(card.Account!, card.Brand, card.CardholderName, null, card.Id, card);
                await _notificationPublisher.PublishAsync(Notify(card.UserId, "Kartica je trajno ukinuta",
                    $"Kartica ...{card.Last4} je ukinuta zbog prijave gubitka ili krađe i više se ne može koristiti." +
                    (created != null ? $" Izdata je zamenska kartica ...{created.Last4}." : string.Empty)));
                await _unitOfWork.SaveChangesAsync();
                return created;
            });
            return replacement == null ? null : await LoadAsync(replacement.Id);
        }

        public async Task<CardAuthorization> AuthorizeAsync(Actor actor, int cardId, CardAuthorizationRequest request)
        {
            var card = await LoadOwnedAsync(cardId, actor);
            MoneyRules.EnsureValidAmount(request.Amount);
            var merchant = string.IsNullOrWhiteSpace(request.MerchantName) ? "Nepoznat trgovac" : request.MerchantName.Trim();
            var authorization = new CardAuthorization
            {
                CardId = card.Id,
                Channel = request.Channel,
                MerchantName = merchant.Length > 80 ? merchant[..80] : merchant,
                CategoryCode = string.IsNullOrWhiteSpace(request.CategoryCode) ? null : request.CategoryCode.Trim().ToUpperInvariant(),
                Amount = request.Amount,
                Currency = card.Account!.Currency,
                IsAbroad = request.IsAbroad,
                CreatedAt = UtcNow
            };

            var decline = await CheckRulesAsync(card, request);
            if (decline == null)
            {
                try
                {
                    authorization.TransactionId = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        var account = (await _accountRepository.LockForUpdateAsync([card.AccountId])).Single();
                        MoneyRules.EnsureActive(account, "Račun uz karticu nije aktivan.");
                        MoneyRules.EnsureSufficientFunds(account, request.Amount);
                        var customerLedger = (await _ledgerRepository.GetCustomerLedgerAccountIdsAsync([account.Id]))[account.Id];
                        var counterpartLedger = await _ledgerRepository.GetSystemLedgerAccountIdAsync(
                            request.Channel == CardChannel.Bankomat ? LedgerAccountType.Blagajna : LedgerAccountType.Kliring, account.Currency);

                        account.Balance -= request.Amount;
                        account.UpdatedAt = UtcNow;
                        var transaction = new Transaction
                        {
                            Amount = request.Amount,
                            Currency = account.Currency,
                            Type = TransactionType.Kartica,
                            Status = TransactionStatus.Uspesna,
                            Description = $"Kartica ...{card.Last4}: {authorization.MerchantName}{(request.Channel == CardChannel.Bankomat ? " (bankomat)" : request.Channel == CardChannel.Internet ? " (internet)" : string.Empty)}",
                            ReferenceNumber = MoneyRules.NewReferenceNumber(UtcNow),
                            PerformedByUserId = actor.UserId,
                            FromAccountId = account.Id,
                            FromBalanceAfter = account.Balance,
                            CreatedAt = UtcNow
                        };
                        await _ledgerRecorder.RecordAsync(transaction,
                            new LedgerLine(customerLedger, -request.Amount, account.Balance),
                            new LedgerLine(counterpartLedger, request.Amount));
                        return (int?)transaction.Id;
                    });
                }
                catch (BusinessRuleException exception)
                {
                    decline = exception.Message;
                }
            }

            authorization.Status = decline == null ? CardAuthorizationStatus.Odobrena : CardAuthorizationStatus.Odbijena;
            authorization.DeclineReason = decline;
            await _context.CardAuthorizations.AddAsync(authorization);
            await _notificationPublisher.PublishAsync(Notify(card.UserId,
                decline == null ? "Plaćanje karticom" : "Odbijena transakcija karticom",
                decline == null
                    ? $"Kartica ...{card.Last4}: {MoneyRules.Format(request.Amount, authorization.Currency)}, {authorization.MerchantName}."
                    : $"Kartica ...{card.Last4}: {MoneyRules.Format(request.Amount, authorization.Currency)} kod {authorization.MerchantName} je odbijeno. Razlog: {decline}"));
            await _context.SaveChangesAsync();
            return authorization;
        }

        public async Task<PagedResult<CardAuthorization>> GetAuthorizationsAsync(Actor actor, int cardId, PageRequest page)
        {
            if (!actor.IsStaff)
                await LoadOwnedAsync(cardId, actor);
            return await _context.CardAuthorizations.AsNoTracking()
                .Where(a => a.CardId == cardId)
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                .ToPagedResultAsync(page);
        }

        private async Task<string?> CheckRulesAsync(Card card, CardAuthorizationRequest request)
        {
            if (card.Status == CardStatus.Blokirana)
                return "Kartica je blokirana.";
            if (card.Status == CardStatus.Ukinuta)
                return "Kartica je ukinuta.";
            if (card.IsExpiredAt(_businessCalendar.Today()))
                return "Kartici je istekao rok važenja.";
            if (request.Channel == CardChannel.Internet && !card.OnlineEnabled)
                return "Plaćanje na internetu je isključeno u podešavanjima kartice.";
            if (request.IsAbroad && !card.AbroadEnabled)
                return "Plaćanje u inostranstvu je isključeno u podešavanjima kartice.";
            if (request.Channel == CardChannel.Prodavnica && request.Contactless && !card.ContactlessEnabled)
                return "Beskontaktno plaćanje je isključeno u podešavanjima kartice.";

            var isAtm = request.Channel == CardChannel.Bankomat;
            var limit = isAtm ? card.DailyAtmLimit : card.DailyPurchaseLimit;
            var startUtc = _businessCalendar.StartOfTodayUtc();
            var spentToday = await _context.CardAuthorizations
                .Where(a => a.CardId == card.Id && a.Status == CardAuthorizationStatus.Odobrena && a.CreatedAt >= startUtc
                            && (isAtm ? a.Channel == CardChannel.Bankomat : a.Channel != CardChannel.Bankomat))
                .SumAsync(a => (decimal?)a.Amount) ?? 0m;
            if (spentToday + request.Amount > limit)
                return $"Prekoračen dnevni limit kartice za {(isAtm ? "bankomat" : "plaćanje")} ({MoneyRules.Format(limit, card.Account!.Currency)}).";
            return null;
        }

        private async Task<Card> CreateCardAsync(Account account, CardBrand brand, string holder, int? issuedBy, int? replaces, Card? previous = null)
        {
            string pan;
            string hash;
            do
            {
                pan = CardNumbers.Generate(brand);
                hash = CardNumbers.Hash(pan);
            }
            while (await _context.Cards.AnyAsync(c => c.PanHash == hash));

            var expiry = _businessCalendar.Today().AddYears(ValidityYears);
            var defaults = CardLimits.Default(account.Currency);
            var card = new Card
            {
                UserId = account.UserId,
                AccountId = account.Id,
                Brand = brand,
                PanHash = hash,
                Last4 = pan[^4..],
                CardholderName = holder.Length > 60 ? holder[..60] : holder,
                ExpiryMonth = expiry.Month,
                ExpiryYear = expiry.Year,
                Status = CardStatus.Aktivna,
                OnlineEnabled = previous?.OnlineEnabled ?? true,
                ContactlessEnabled = previous?.ContactlessEnabled ?? true,
                AbroadEnabled = previous?.AbroadEnabled ?? false,
                DailyPurchaseLimit = previous?.DailyPurchaseLimit ?? defaults.Purchase,
                DailyAtmLimit = previous?.DailyAtmLimit ?? defaults.Atm,
                ReplacesCardId = replaces,
                IssuedByUserId = issuedBy,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };
            await _context.Cards.AddAsync(card);
            await _context.SaveChangesAsync();
            return card;
        }

        private IQueryable<Card> QueryForUser(int userId) =>
            _context.Cards.AsNoTracking().Include(c => c.Account)
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Status == CardStatus.Ukinuta)
                .ThenByDescending(c => c.CreatedAt);

        private async Task<Card> LoadOwnedAsync(int cardId, Actor actor)
        {
            EnsureCustomer(actor);
            return await _context.Cards.Include(c => c.Account).FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == actor.UserId)
                ?? throw new NotFoundException("Kartica nije pronađena.");
        }

        private async Task<Card> LoadAsync(int cardId) => await _context.Cards.AsNoTracking().Include(c => c.Account).FirstAsync(c => c.Id == cardId);

        private Notification Notify(int userId, string title, string message) => new()
        {
            UserId = userId,
            Type = NotificationType.Kartica,
            Title = title,
            Message = message,
            CreatedAt = UtcNow
        };

        private static void EnsureCustomer(Actor actor)
        {
            if (!actor.IsCustomer)
                throw new ForbiddenException("Ovu radnju sa karticom obavlja vlasnik kartice.");
        }
    }
}
