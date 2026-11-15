using System.Security.Cryptography;
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
    public class CurrencyExchangeService : ICurrencyExchangeService
    {
        public const decimal MaxSpreadPercent = 5m;

        private readonly ApplicationDbContext _context;
        private readonly IAccountRepository _accountRepository;
        private readonly ILedgerRepository _ledgerRepository;
        private readonly LedgerRecorder _ledgerRecorder;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly TimeProvider _timeProvider;

        public CurrencyExchangeService(
            ApplicationDbContext context,
            IAccountRepository accountRepository,
            ILedgerRepository ledgerRepository,
            LedgerRecorder ledgerRecorder,
            IUnitOfWork unitOfWork,
            INotificationPublisher notificationPublisher,
            TimeProvider timeProvider)
        {
            _context = context;
            _accountRepository = accountRepository;
            _ledgerRepository = ledgerRepository;
            _ledgerRecorder = ledgerRecorder;
            _unitOfWork = unitOfWork;
            _notificationPublisher = notificationPublisher;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<IReadOnlyList<ExchangeRate>> GetCurrentRatesAsync()
        {
            var now = UtcNow;
            var recent = await _context.ExchangeRates.AsNoTracking()
                .Where(r => r.ValidFrom <= now)
                .OrderByDescending(r => r.ValidFrom)
                .ThenByDescending(r => r.Id)
                .Take(500)
                .ToListAsync();
            return recent.GroupBy(r => r.Currency).Select(g => g.First()).OrderBy(r => r.Currency).ToList();
        }

        public async Task<IReadOnlyList<ExchangeRate>> GetHistoryAsync(string currency, int days)
        {
            var since = UtcNow.AddDays(-Math.Clamp(days, 1, 365));
            var code = currency.Trim().ToUpperInvariant();
            return await _context.ExchangeRates.AsNoTracking()
                .Where(r => r.Currency == code && r.ValidFrom >= since)
                .OrderByDescending(r => r.ValidFrom)
                .Take(200)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ExchangeRate>> PublishRatesAsync(IReadOnlyList<ExchangeRateInput> rates, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();
            if (rates.Count == 0)
                throw new BusinessRuleException("Kursna lista mora sadržati bar jednu valutu.");
            if (rates.Select(r => r.Currency.ToUpperInvariant()).Distinct().Count() != rates.Count)
                throw new BusinessRuleException("Svaka valuta može se navesti samo jednom.");

            var current = (await GetCurrentRatesAsync()).ToDictionary(r => r.Currency);
            var validFrom = new DateTime(UtcNow.Ticks - UtcNow.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
            var published = new List<ExchangeRate>();
            foreach (var input in rates)
            {
                var currency = input.Currency.Trim().ToUpperInvariant();
                if (currency == CurrencyConversion.DomesticCurrency)
                    throw new BusinessRuleException("Kurs se ne objavljuje za dinar.");
                if (!(input.BuyRate > 0 && input.BuyRate < input.MiddleRate && input.MiddleRate < input.SellRate))
                    throw new BusinessRuleException($"{currency}: mora važiti kupovni < srednji < prodajni kurs.");
                if ((input.SellRate - input.BuyRate) / input.MiddleRate * 100m > MaxSpreadPercent * 2)
                    throw new BusinessRuleException($"{currency}: razlika između kupovnog i prodajnog kursa ne sme biti veća od {MaxSpreadPercent * 2}% srednjeg kursa.");
                if (current.TryGetValue(currency, out var previous) && Math.Abs(input.MiddleRate - previous.MiddleRate) / previous.MiddleRate > 0.2m)
                    throw new BusinessRuleException($"{currency}: srednji kurs se razlikuje više od 20% od važećeg. Proverite unos.");

                var rate = new ExchangeRate
                {
                    Currency = currency,
                    BuyRate = decimal.Round(input.BuyRate, 4),
                    MiddleRate = decimal.Round(input.MiddleRate, 4),
                    SellRate = decimal.Round(input.SellRate, 4),
                    ValidFrom = validFrom,
                    CreatedByUserId = actor.UserId,
                    CreatedAt = UtcNow
                };
                await _context.ExchangeRates.AddAsync(rate);
                published.Add(rate);
            }
            await _context.SaveChangesAsync();
            return published;
        }

        public async Task<ExchangeQuote> QuoteAsync(CurrencyExchangeCommand command, Actor actor)
        {
            var (from, to) = await LoadPairAsync(command, actor, lockAccounts: false);
            var rate = await GetRateForPairAsync(from.Currency, to.Currency);
            var result = Calculate(from, to, command, rate);
            return new ExchangeQuote(from.Id, to.Id, from.Currency, to.Currency, result.FromAmount, result.ToAmount, result.AppliedRate,
                result.RateType, result.MiddleRate, result.SpreadRsd, rate.Id, rate.ValidFrom, from.Balance - result.FromAmount);
        }

        public async Task<CurrencyExchangeResult> ExecuteAsync(CurrencyExchangeCommand command, Actor actor)
        {
            if (command.IdempotencyKey != null)
            {
                var existing = await FindByKeyAsync(actor.UserId, command.IdempotencyKey);
                if (existing != null)
                    return new CurrencyExchangeResult(existing, true);
            }

            try
            {
                var exchange = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var (from, to) = await LoadPairAsync(command, actor, lockAccounts: true);
                    var rate = await GetRateForPairAsync(from.Currency, to.Currency);
                    if (command.ExpectedRateId.HasValue && command.ExpectedRateId.Value != rate.Id)
                        throw new ConflictException("Kurs je u međuvremenu promenjen. Proverite novi obračun pre potvrde.");

                    var result = Calculate(from, to, command, rate);
                    MoneyRules.EnsureActive(from, "Račun sa kog menjate nije aktivan.");
                    MoneyRules.EnsureActive(to, "Račun na koji menjate nije aktivan.");
                    MoneyRules.EnsureSufficientFunds(from, result.FromAmount);

                    var ledgerIds = await _ledgerRepository.GetCustomerLedgerAccountIdsAsync([from.Id, to.Id]);
                    var positionFrom = await _ledgerRepository.GetSystemLedgerAccountIdAsync(LedgerAccountType.Menjacnica, from.Currency);
                    var positionTo = await _ledgerRepository.GetSystemLedgerAccountIdAsync(LedgerAccountType.Menjacnica, to.Currency);
                    var income = await _ledgerRepository.GetSystemLedgerAccountIdAsync(LedgerAccountType.Provizije, CurrencyConversion.DomesticCurrency);
                    var reference = $"MEN-{UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}";
                    var description = $"Menjačnica {reference}: {MoneyRules.Format(result.FromAmount, from.Currency)} u {MoneyRules.Format(result.ToAmount, to.Currency)}, kurs {result.AppliedRate:0.0000}";

                    from.Balance -= result.FromAmount;
                    from.UpdatedAt = UtcNow;
                    to.Balance += result.ToAmount;
                    to.UpdatedAt = UtcNow;

                    var source = NewTransaction(actor, result.FromAmount, from.Currency, description);
                    source.FromAccountId = from.Id;
                    source.FromBalanceAfter = from.Balance;
                    var target = NewTransaction(actor, result.ToAmount, to.Currency, description);
                    target.ToAccountId = to.Id;
                    target.ToBalanceAfter = to.Balance;

                    var spread = result.SpreadRsd;
                    if (from.Currency == CurrencyConversion.DomesticCurrency)
                    {
                        await _ledgerRecorder.RecordAsync(source, Lines(
                            new LedgerLine(ledgerIds[from.Id], -result.FromAmount, from.Balance),
                            new LedgerLine(positionFrom, result.FromAmount - spread),
                            spread > 0 ? new LedgerLine(income, spread) : null));
                        await _ledgerRecorder.RecordAsync(target,
                            new LedgerLine(ledgerIds[to.Id], result.ToAmount, to.Balance),
                            new LedgerLine(positionTo, -result.ToAmount));
                    }
                    else
                    {
                        await _ledgerRecorder.RecordAsync(source,
                            new LedgerLine(ledgerIds[from.Id], -result.FromAmount, from.Balance),
                            new LedgerLine(positionFrom, result.FromAmount));
                        await _ledgerRecorder.RecordAsync(target, Lines(
                            new LedgerLine(ledgerIds[to.Id], result.ToAmount, to.Balance),
                            new LedgerLine(positionTo, -(result.ToAmount + spread)),
                            spread > 0 ? new LedgerLine(income, spread) : null));
                    }

                    var exchange = new CurrencyExchange
                    {
                        ReferenceNumber = reference,
                        UserId = actor.UserId,
                        FromAccountId = from.Id,
                        ToAccountId = to.Id,
                        FromCurrency = from.Currency,
                        ToCurrency = to.Currency,
                        FromAmount = result.FromAmount,
                        ToAmount = result.ToAmount,
                        AppliedRate = result.AppliedRate,
                        RateType = result.RateType,
                        MiddleRate = result.MiddleRate,
                        SpreadRsd = spread,
                        ExchangeRateId = rate.Id,
                        SourceTransactionId = source.Id,
                        TargetTransactionId = target.Id,
                        IdempotencyKey = command.IdempotencyKey,
                        CreatedAt = UtcNow
                    };
                    await _context.CurrencyExchanges.AddAsync(exchange);
                    await _notificationPublisher.PublishAsync(new Notification
                    {
                        UserId = actor.UserId,
                        Type = NotificationType.Konverzija,
                        Title = "Konverzija je izvršena",
                        Message = $"Zamenjeno je {MoneyRules.Format(result.FromAmount, from.Currency)} za {MoneyRules.Format(result.ToAmount, to.Currency)} " +
                                  $"po {(result.RateType == ExchangeRateType.Kupovni ? "kupovnom" : "prodajnom")} kursu {result.AppliedRate:0.0000}.",
                        CreatedAt = UtcNow
                    });
                    await _unitOfWork.SaveChangesAsync();
                    return exchange;
                });
                return new CurrencyExchangeResult(exchange, false);
            }
            catch (DuplicateEntryException) when (command.IdempotencyKey != null)
            {
                var existing = await FindByKeyAsync(actor.UserId, command.IdempotencyKey)
                    ?? throw new ConflictException("Zahtev sa istim ključem je već u obradi. Pokušajte ponovo.");
                return new CurrencyExchangeResult(existing, true);
            }
        }

        public async Task<PagedResult<CurrencyExchange>> GetMineAsync(Actor actor, PageRequest page)
        {
            EnsureCustomer(actor);
            return await _context.CurrencyExchanges.AsNoTracking()
                .Include(x => x.FromAccount)
                .Include(x => x.ToAccount)
                .Where(x => x.UserId == actor.UserId)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .ToPagedResultAsync(page);
        }

        private async Task<(Account From, Account To)> LoadPairAsync(CurrencyExchangeCommand command, Actor actor, bool lockAccounts)
        {
            EnsureCustomer(actor);
            MoneyRules.EnsureValidAmount(command.Amount);
            if (command.FromAccountId == command.ToAccountId)
                throw new BusinessRuleException("Izaberite dva različita računa.");

            Account? from;
            Account? to;
            if (lockAccounts)
            {
                var locked = await _accountRepository.LockForUpdateAsync([command.FromAccountId, command.ToAccountId]);
                from = locked.FirstOrDefault(a => a.Id == command.FromAccountId);
                to = locked.FirstOrDefault(a => a.Id == command.ToAccountId);
            }
            else
            {
                from = await _accountRepository.GetByIdAsync(command.FromAccountId);
                to = await _accountRepository.GetByIdAsync(command.ToAccountId);
            }

            if (from == null || to == null || from.UserId != actor.UserId || to.UserId != actor.UserId)
                throw new NotFoundException("Račun nije pronađen.");
            if (from.Currency == to.Currency)
                throw new BusinessRuleException("Računi su u istoj valuti. Za prenos koristite plaćanje.");
            if (from.Currency != CurrencyConversion.DomesticCurrency && to.Currency != CurrencyConversion.DomesticCurrency)
                throw new BusinessRuleException("Konverzija je moguća samo između dinarskog i deviznog računa. Za EUR i USD promenite preko dinarskog računa.");
            return (from, to);
        }

        private async Task<ExchangeRate> GetRateForPairAsync(string fromCurrency, string toCurrency)
        {
            var foreign = fromCurrency == CurrencyConversion.DomesticCurrency ? toCurrency : fromCurrency;
            return (await GetCurrentRatesAsync()).FirstOrDefault(r => r.Currency == foreign)
                ?? throw new BusinessRuleException($"Za valutu {foreign} trenutno nema važećeg kursa.");
        }

        private static ConversionResult Calculate(Account from, Account to, CurrencyExchangeCommand command, ExchangeRate rate)
        {
            var result = CurrencyConversion.Calculate(from.Currency, to.Currency, command.Amount, command.AmountIsTarget, rate);
            if (result.FromAmount <= 0 || result.ToAmount <= 0)
                throw new BusinessRuleException("Iznos je premali za konverziju.");
            MoneyRules.EnsureValidAmount(result.FromAmount);
            return result;
        }

        private Transaction NewTransaction(Actor actor, decimal amount, string currency, string description) => new()
        {
            Amount = amount,
            Currency = currency,
            Type = TransactionType.Konverzija,
            Status = TransactionStatus.Uspesna,
            Description = description.Length > 255 ? description[..255] : description,
            ReferenceNumber = MoneyRules.NewReferenceNumber(UtcNow),
            PerformedByUserId = actor.UserId,
            CreatedAt = UtcNow
        };

        private static LedgerLine[] Lines(params LedgerLine?[] lines) => lines.Where(l => l != null).Select(l => l!).ToArray();

        private Task<CurrencyExchange?> FindByKeyAsync(int userId, string key) =>
            _context.CurrencyExchanges.AsNoTracking()
                .Include(x => x.FromAccount)
                .Include(x => x.ToAccount)
                .FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == key);

        private static void EnsureCustomer(Actor actor)
        {
            if (!actor.IsCustomer)
                throw new ForbiddenException("Menjačnicu koristi samo vlasnik računa.");
        }
    }
}
