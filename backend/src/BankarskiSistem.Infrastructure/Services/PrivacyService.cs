using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Services
{
    public class PrivacyService : IPrivacyService
    {
        public static readonly string[] SecurityActions =
        [
            "Uspesna prijava",
            StatisticsService.FailedLoginAction,
            "Promena lozinke",
            "Izmenjen profil",
            "Izvoz licnih podataka",
            "Prihvaceni pravni dokumenti"
        ];

        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;

        public PrivacyService(ApplicationDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<PersonalDataExport> ExportAsync(int userId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new NotFoundException("Korisnik nije pronađen.");

            var accounts = await _context.Accounts.AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => new { a.AccountNumber, a.Currency, a.Balance, Status = a.Status.ToString(), a.CreatedAt })
                .ToListAsync();

            var transactions = await _context.Transactions.AsNoTracking()
                .Where(t => (t.FromAccount != null && t.FromAccount.UserId == userId) || (t.ToAccount != null && t.ToAccount.UserId == userId))
                .OrderBy(t => t.CreatedAt)
                .Select(t => new
                {
                    t.ReferenceNumber,
                    Type = t.Type.ToString(),
                    t.Amount,
                    t.Currency,
                    FromAccount = t.FromAccount != null ? t.FromAccount.AccountNumber : null,
                    ToAccount = t.ToAccount != null ? t.ToAccount.AccountNumber : null,
                    t.Description,
                    t.CreatedAt
                })
                .ToListAsync();

            var orders = await _context.PaymentOrders.AsNoTracking()
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new
                {
                    p.RecipientName, p.RecipientAccountNumber, p.Amount, p.Currency, p.PaymentCode, p.Purpose,
                    p.ReferenceModel, p.ReferenceNumber, Status = p.Status.ToString(), p.ScheduledFor, p.ExecutedAt, p.CreatedAt
                })
                .ToListAsync();

            var beneficiaries = await _context.Beneficiaries.AsNoTracking()
                .Where(b => b.UserId == userId)
                .Select(b => new { b.Nickname, b.RecipientName, b.AccountNumber, b.CreatedAt })
                .ToListAsync();

            var consents = await _context.UserConsents.AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => new { Document = c.LegalDocument!.Title, c.LegalDocument.Version, c.AcceptedAt, c.IpAddress })
                .ToListAsync();

            var notifications = await _context.Notifications.AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderBy(n => n.CreatedAt)
                .Select(n => new { n.Title, n.Message, n.IsRead, n.CreatedAt })
                .ToListAsync();

            var security = await _context.AuditLogs.AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.CreatedAt)
                .Select(a => new { a.Action, a.IsSuccess, a.IpAddress, a.CreatedAt })
                .ToListAsync();

            return new PersonalDataExport(
                _timeProvider.GetUtcNow().UtcDateTime,
                new { user.FirstName, user.LastName, user.Email, Role = user.Role.ToString(), user.CreatedAt },
                accounts.Cast<object>().ToList(),
                transactions.Cast<object>().ToList(),
                orders.Cast<object>().ToList(),
                beneficiaries.Cast<object>().ToList(),
                consents.Cast<object>().ToList(),
                notifications.Cast<object>().ToList(),
                security.Cast<object>().ToList());
        }

        public Task<PagedResult<SecurityEvent>> GetSecurityEventsAsync(int userId, PageRequest page) =>
            _context.AuditLogs.AsNoTracking()
                .Where(a => a.UserId == userId && SecurityActions.Contains(a.Action))
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Select(a => new SecurityEvent(a.CreatedAt, a.Action, a.IsSuccess, a.IpAddress))
                .ToPagedResultAsync(page);
    }

    public class LedgerQueryService : ILedgerQueryService
    {
        private readonly ApplicationDbContext _context;

        public LedgerQueryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<TrialBalanceLine>> GetTrialBalanceAsync()
        {
            var lines = await _context.Postings.AsNoTracking()
                .Where(p => _context.JournalEntries.Any(j => j.Id == p.JournalEntryId && j.IsPosted))
                .GroupBy(p => p.Currency)
                .Select(g => new { Currency = g.Key, Total = g.Sum(p => p.Amount), Count = g.Count() })
                .ToListAsync();

            return lines
                .OrderBy(l => l.Currency)
                .Select(l => new TrialBalanceLine(l.Currency, l.Total, l.Count))
                .ToList();
        }

        public async Task<IReadOnlyList<LedgerAccountBalance>> GetLedgerAccountBalancesAsync(bool systemOnly)
        {
            var query = _context.LedgerAccounts.AsNoTracking().AsQueryable();
            if (systemOnly)
                query = query.Where(l => l.AccountId == null);

            return await query
                .OrderBy(l => l.Type).ThenBy(l => l.Currency).ThenBy(l => l.Code)
                .Select(l => new LedgerAccountBalance(
                    l.Id, l.Code, l.Name, l.Type, l.Currency,
                    _context.Postings
                        .Where(p => p.LedgerAccountId == l.Id && _context.JournalEntries.Any(j => j.Id == p.JournalEntryId && j.IsPosted))
                        .Sum(p => (decimal?)p.Amount) ?? 0m))
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ReconciliationLine>> GetReconciliationAsync(bool onlyDifferences)
        {
            var lines = await _context.Accounts.AsNoTracking()
                .Select(a => new
                {
                    a.Id, a.AccountNumber, a.Currency, a.Balance,
                    Ledger = _context.Postings
                        .Where(p => _context.LedgerAccounts.Any(l => l.Id == p.LedgerAccountId && l.AccountId == a.Id)
                                    && _context.JournalEntries.Any(j => j.Id == p.JournalEntryId && j.IsPosted))
                        .Sum(p => (decimal?)p.Amount) ?? 0m
                })
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();

            return lines
                .Select(l => new ReconciliationLine(l.Id, l.AccountNumber, l.Currency, l.Balance, l.Ledger, l.Balance - l.Ledger))
                .Where(l => !onlyDifferences || l.Difference != 0)
                .ToList();
        }
    }
}
