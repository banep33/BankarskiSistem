using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using BankarskiSistem.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Services
{
    public static class ComplianceRules
    {
        public const string CashThreshold = "GOTOVINA_PREKO_PRAGA";
        public const string CashStructuring = "STRUKTURIRANJE_GOTOVINE";
        public const string RapidOutgoing = "BRZE_UZASTOPNE_ISPLATE";
        public const string NewPayeeLargeAmount = "NOVI_PRIMALAC_VELIKI_IZNOS";
        public const string HighRiskCustomer = "VISOKORIZICAN_KLIJENT";
    }

    public class ComplianceMonitor : IComplianceMonitor
    {
        private readonly ApplicationDbContext _context;
        private readonly ComplianceSettings _settings;
        private readonly TimeProvider _timeProvider;

        public ComplianceMonitor(ApplicationDbContext context, IOptions<ComplianceSettings> settings, TimeProvider timeProvider)
        {
            _context = context;
            _settings = settings.Value;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task EvaluateAsync(Transaction transaction, Account account, string? recipientAccountNumber)
        {
            var owner = await _context.Users.AsNoTracking()
                .Where(u => u.Id == account.UserId)
                .Select(u => new { u.Id, u.RiskRating })
                .SingleAsync();

            var alerts = new List<ComplianceAlert>();
            var isCash = transaction.Type is TransactionType.Uplata or TransactionType.Isplata;
            var isOutgoing = transaction.FromAccountId == account.Id;

            if (isCash)
                await EvaluateCashRulesAsync(transaction, account, alerts);

            if (isOutgoing && !isCash)
            {
                await EvaluateVelocityAsync(transaction, account, alerts);
                await EvaluateNewPayeeAsync(transaction, account, recipientAccountNumber, alerts);
            }

            var largeThreshold = Threshold(_settings.LargePaymentThresholds, transaction.Currency);
            if (owner.RiskRating == RiskLevel.Visok && isOutgoing && largeThreshold.HasValue && transaction.Amount >= largeThreshold)
            {
                alerts.Add(Alert(ComplianceRules.HighRiskCustomer, RiskLevel.Visok, transaction, account,
                    $"Klijent sa visokim rizikom izvršio je odlaznu transakciju od {MoneyRules.Format(transaction.Amount, transaction.Currency)}."));
            }

            await _context.ComplianceAlerts.AddRangeAsync(alerts);
        }

        private async Task EvaluateCashRulesAsync(Transaction transaction, Account account, List<ComplianceAlert> alerts)
        {
            var threshold = Threshold(_settings.CashReportingThresholds, transaction.Currency);
            if (!threshold.HasValue)
                return;

            if (transaction.Amount >= threshold)
            {
                alerts.Add(Alert(ComplianceRules.CashThreshold, RiskLevel.Visok, transaction, account,
                    $"Gotovinska transakcija od {MoneyRules.Format(transaction.Amount, transaction.Currency)} dostiže prag za izveštavanje " +
                    $"({MoneyRules.Format(threshold.Value, transaction.Currency)}). U stvarnoj banci podaci se dostavljaju Upravi za sprečavanje pranja novca."));
                return;
            }

            var windowStart = UtcNow.AddHours(-_settings.StructuringWindowHours);
            var recentCash = await _context.Transactions.AsNoTracking()
                .Where(t => (t.FromAccountId == account.Id || t.ToAccountId == account.Id)
                            && (t.Type == TransactionType.Uplata || t.Type == TransactionType.Isplata)
                            && t.Status == TransactionStatus.Uspesna
                            && t.CreatedAt >= windowStart
                            && t.Id != transaction.Id)
                .Select(t => t.Amount)
                .ToListAsync();

            var operations = recentCash.Count + 1;
            var total = recentCash.Sum() + transaction.Amount;
            if (operations >= _settings.StructuringMinimumOperations && total >= threshold)
            {
                alerts.Add(Alert(ComplianceRules.CashStructuring, RiskLevel.Visok, transaction, account,
                    $"{operations} gotovinskih transakcija u poslednjih {_settings.StructuringWindowHours} h, ukupno " +
                    $"{MoneyRules.Format(total, transaction.Currency)}, pojedinačno ispod praga. Moguće izbegavanje izveštavanja."));
            }
        }

        private async Task EvaluateVelocityAsync(Transaction transaction, Account account, List<ComplianceAlert> alerts)
        {
            var windowStart = UtcNow.AddMinutes(-_settings.RapidPaymentWindowMinutes);
            var alreadyFlagged = await _context.ComplianceAlerts.AnyAsync(a =>
                a.RuleCode == ComplianceRules.RapidOutgoing && a.AccountId == account.Id && a.CreatedAt >= windowStart);
            if (alreadyFlagged)
                return;

            var count = await _context.Transactions.CountAsync(t =>
                t.FromAccountId == account.Id && t.CreatedAt >= windowStart && t.Status == TransactionStatus.Uspesna) + 1;

            if (count >= _settings.RapidPaymentCount)
            {
                alerts.Add(Alert(ComplianceRules.RapidOutgoing, RiskLevel.Srednji, transaction, account,
                    $"{count} odlaznih plaćanja za {_settings.RapidPaymentWindowMinutes} minuta sa istog računa."));
            }
        }

        private async Task EvaluateNewPayeeAsync(Transaction transaction, Account account, string? recipientAccountNumber, List<ComplianceAlert> alerts)
        {
            var threshold = Threshold(_settings.LargePaymentThresholds, transaction.Currency);
            if (!threshold.HasValue || transaction.Amount < threshold || string.IsNullOrEmpty(recipientAccountNumber))
                return;

            var paidBefore = await _context.PaymentOrders.AnyAsync(p =>
                p.UserId == account.UserId
                && p.RecipientAccountNumber == recipientAccountNumber
                && p.TransactionId != null
                && p.TransactionId != transaction.Id);

            if (!paidBefore)
            {
                alerts.Add(Alert(ComplianceRules.NewPayeeLargeAmount, RiskLevel.Srednji, transaction, account,
                    $"Prvo plaćanje primaocu {recipientAccountNumber} u iznosu od {MoneyRules.Format(transaction.Amount, transaction.Currency)}."));
            }
        }

        private ComplianceAlert Alert(string rule, RiskLevel severity, Transaction transaction, Account account, string details) => new()
        {
            RuleCode = rule,
            Severity = severity,
            UserId = account.UserId,
            AccountId = account.Id,
            TransactionId = transaction.Id,
            Details = details.Length > 1000 ? details[..1000] : details,
            CreatedAt = UtcNow
        };

        private static decimal? Threshold(Dictionary<string, decimal> thresholds, string currency) =>
            thresholds.TryGetValue(currency, out var value) ? value : null;
    }

    public class ComplianceService : IComplianceService
    {
        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;

        public ComplianceService(ApplicationDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public Task<PagedResult<ComplianceAlert>> GetPagedAsync(ComplianceAlertStatus? status, RiskLevel? severity, PageRequest page, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();

            var query = _context.ComplianceAlerts.AsNoTracking().Include(a => a.User).AsQueryable();
            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);
            if (severity.HasValue)
                query = query.Where(a => a.Severity == severity.Value);

            return query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).ToPagedResultAsync(page);
        }

        public async Task<ComplianceAlert> ReviewAsync(int alertId, ComplianceAlertStatus status, string note, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();
            if (status == ComplianceAlertStatus.Otvoren)
                throw new BusinessRuleException("Upozorenje se ne može vratiti u status Otvoren.");
            if (string.IsNullOrWhiteSpace(note))
                throw new BusinessRuleException("Obrazloženje provere je obavezno.");

            var alert = await _context.ComplianceAlerts.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == alertId)
                ?? throw new NotFoundException("Upozorenje nije pronađeno.");

            if (alert.Status is ComplianceAlertStatus.ZatvorenBezNalaza or ComplianceAlertStatus.EskaliranSimulacija)
                throw new BusinessRuleException("Upozorenje je već zatvoreno.");

            alert.Status = status;
            alert.ReviewNote = note.Trim();
            alert.ReviewedByUserId = actor.UserId;
            alert.ReviewedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _context.SaveChangesAsync();
            return alert;
        }
    }
}
