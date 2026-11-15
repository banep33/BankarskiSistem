using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Services
{
    public class StatisticsService : IStatisticsService
    {
        private const int RecentTransactionsCount = 6;

        private readonly ApplicationDbContext _context;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBusinessCalendar _businessCalendar;
        private readonly TimeProvider _timeProvider;

        public StatisticsService(
            ApplicationDbContext context,
            ITransactionRepository transactionRepository,
            IBusinessCalendar businessCalendar,
            TimeProvider timeProvider)
        {
            _context = context;
            _transactionRepository = transactionRepository;
            _businessCalendar = businessCalendar;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<CustomerDashboard> GetCustomerDashboardAsync(int userId)
        {
            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => new { a.Id, a.Currency, a.Balance, a.Status })
                .ToListAsync();

            var accountIds = accounts.Select(a => a.Id).ToList();
            var monthStartUtc = StartOfCurrentMonthUtc();

            var monthMovements = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.Status == TransactionStatus.Uspesna
                            && t.CreatedAt >= monthStartUtc
                            && ((t.FromAccountId != null && accountIds.Contains(t.FromAccountId.Value))
                                || (t.ToAccountId != null && accountIds.Contains(t.ToAccountId.Value))))
                .Select(t => new { t.FromAccountId, t.ToAccountId, t.Amount, t.Currency })
                .ToListAsync();

            bool IsOwn(int? accountId) => accountId.HasValue && accountIds.Contains(accountId.Value);

            var cashFlow = monthMovements
                .GroupBy(t => t.Currency)
                .Select(g => new CashFlowSummary(
                    g.Key,
                    g.Where(t => IsOwn(t.ToAccountId) && !IsOwn(t.FromAccountId)).Sum(t => t.Amount),
                    g.Where(t => IsOwn(t.FromAccountId) && !IsOwn(t.ToAccountId)).Sum(t => t.Amount)))
                .OrderBy(c => c.Currency)
                .ToList();

            var balances = accounts
                .GroupBy(a => a.Currency)
                .Select(g => new CurrencyAmount(g.Key, g.Sum(a => a.Balance)))
                .OrderBy(b => b.Currency)
                .ToList();

            var unread = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            var recent = await _transactionRepository.GetRecentForUserAsync(userId, RecentTransactionsCount);

            return new CustomerDashboard(
                balances,
                cashFlow,
                accounts.Count(a => a.Status == AccountStatus.Aktivan),
                accounts.Count,
                unread,
                recent);
        }

        public async Task<AccountStatistics> GetAccountStatisticsAsync(int accountId, StatisticsPeriod period, Actor actor)
        {
            var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId)
                ?? throw new NotFoundException("Račun nije pronađen.");
            AccountAccessPolicy.EnsureCanView(account, actor);

            var todayLocal = _businessCalendar.ToBusinessTime(UtcNow).Date;
            var bucketStarts = BuildBucketStarts(period, todayLocal);
            var rangeStartUtc = _businessCalendar.ToUtc(bucketStarts[0]);

            var movements = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.Status == TransactionStatus.Uspesna
                            && t.CreatedAt >= rangeStartUtc
                            && (t.FromAccountId == accountId || t.ToAccountId == accountId))
                .Select(t => new { t.CreatedAt, t.Amount, t.FromAccountId, t.ToAccountId })
                .ToListAsync();

            var buckets = bucketStarts.Select((start, index) =>
            {
                var end = index + 1 < bucketStarts.Count ? bucketStarts[index + 1] : NextBucketStart(period, start);
                var inBucket = movements
                    .Where(m =>
                    {
                        var local = _businessCalendar.ToBusinessTime(m.CreatedAt);
                        return local >= start && local < end;
                    })
                    .ToList();

                return new CashFlowBucket(
                    start,
                    inBucket.Where(m => m.ToAccountId == accountId).Sum(m => m.Amount),
                    inBucket.Where(m => m.FromAccountId == accountId).Sum(m => m.Amount),
                    inBucket.Count);
            }).ToList();

            return new AccountStatistics(
                account.Id,
                account.Currency,
                period,
                buckets.Sum(b => b.Income),
                buckets.Sum(b => b.Expense),
                buckets);
        }

        public async Task<SystemOverview> GetSystemOverviewAsync()
        {
            var now = UtcNow;

            var userStats = await _context.Users
                .AsNoTracking()
                .GroupBy(u => u.Role)
                .Select(g => new
                {
                    Role = g.Key,
                    Total = g.Count(),
                    Active = g.Count(u => u.IsActive),
                    Locked = g.Count(u => u.IsLocked && u.LockedUntil > now)
                })
                .ToListAsync();

            var accountStats = await _context.Accounts
                .AsNoTracking()
                .GroupBy(a => new { a.Currency, a.Status })
                .Select(g => new { g.Key.Currency, g.Key.Status, Count = g.Count(), Balance = g.Sum(a => a.Balance) })
                .ToListAsync();

            var todayStartUtc = _businessCalendar.StartOfTodayUtc();
            var todayVolume = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.Status == TransactionStatus.Uspesna && t.CreatedAt >= todayStartUtc)
                .GroupBy(t => t.Currency)
                .Select(g => new { Currency = g.Key, Count = g.Count(), Amount = g.Sum(t => t.Amount) })
                .ToListAsync();

            var failedLogins = await _context.AuditLogs
                .CountAsync(a => !a.IsSuccess && a.Action == AuditActions.FailedLogin && a.CreatedAt >= now.AddHours(-24));

            var activity = await BuildActivityLast7DaysAsync();

            int UsersInRole(UserRole role) => userStats.Where(s => s.Role == role).Sum(s => s.Total);

            return new SystemOverview(
                userStats.Sum(s => s.Total),
                UsersInRole(UserRole.Korisnik),
                UsersInRole(UserRole.Bankar),
                UsersInRole(UserRole.Administrator),
                userStats.Sum(s => s.Active),
                userStats.Sum(s => s.Locked),
                accountStats.Sum(s => s.Count),
                accountStats.Where(s => s.Status == AccountStatus.Aktivan).Sum(s => s.Count),
                accountStats.Where(s => s.Status == AccountStatus.Blokiran).Sum(s => s.Count),
                accountStats
                    .GroupBy(s => s.Currency)
                    .Select(g => new CurrencyAmount(g.Key, g.Sum(s => s.Balance)))
                    .OrderBy(c => c.Currency)
                    .ToList(),
                todayVolume.Sum(v => v.Count),
                todayVolume.Select(v => new CurrencyAmount(v.Currency, v.Amount)).OrderBy(c => c.Currency).ToList(),
                failedLogins,
                activity);
        }

        private async Task<IReadOnlyList<ActivityBucket>> BuildActivityLast7DaysAsync()
        {
            var todayLocal = _businessCalendar.ToBusinessTime(UtcNow).Date;
            var firstDay = todayLocal.AddDays(-6);
            var rangeStartUtc = _businessCalendar.ToUtc(firstDay);

            var movements = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.Status == TransactionStatus.Uspesna && t.CreatedAt >= rangeStartUtc)
                .Select(t => new { t.CreatedAt, t.Type })
                .ToListAsync();

            var byDay = movements
                .GroupBy(m => _businessCalendar.ToBusinessTime(m.CreatedAt).Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            return Enumerable.Range(0, 7)
                .Select(offset =>
                {
                    var day = firstDay.AddDays(offset);
                    var items = byDay.GetValueOrDefault(day) ?? [];
                    return new ActivityBucket(
                        day,
                        items.Count(i => i.Type == TransactionType.Uplata),
                        items.Count(i => i.Type == TransactionType.Isplata),
                        items.Count(i => i.Type is TransactionType.Transfer or TransactionType.EksterniTransfer));
                })
                .ToList();
        }

        private DateTime StartOfCurrentMonthUtc()
        {
            var localNow = _businessCalendar.ToBusinessTime(UtcNow);
            return _businessCalendar.ToUtc(new DateTime(localNow.Year, localNow.Month, 1));
        }

        private static List<DateTime> BuildBucketStarts(StatisticsPeriod period, DateTime todayLocal) =>
            period switch
            {
                StatisticsPeriod.Week => Enumerable.Range(0, 7).Select(i => todayLocal.AddDays(i - 6)).ToList(),
                StatisticsPeriod.Month => Enumerable.Range(0, 30).Select(i => todayLocal.AddDays(i - 29)).ToList(),
                _ => Enumerable.Range(0, 12)
                    .Select(i => new DateTime(todayLocal.Year, todayLocal.Month, 1).AddMonths(i - 11))
                    .ToList()
            };

        private static DateTime NextBucketStart(StatisticsPeriod period, DateTime start) =>
            period == StatisticsPeriod.Year ? start.AddMonths(1) : start.AddDays(1);
    }
}
