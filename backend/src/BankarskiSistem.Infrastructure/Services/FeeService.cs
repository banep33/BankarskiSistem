using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BankarskiSistem.Infrastructure.Services
{
    public class FeeService : IFeeService
    {
        private static readonly TimeSpan ScheduleCacheDuration = TimeSpan.FromSeconds(60);

        private readonly ApplicationDbContext _context;
        private readonly IBusinessCalendar _businessCalendar;
        private readonly IMemoryCache? _cache;

        public FeeService(ApplicationDbContext context, IBusinessCalendar businessCalendar, IMemoryCache? cache = null)
        {
            _context = context;
            _businessCalendar = businessCalendar;
            _cache = cache;
        }

        public async Task<IReadOnlyList<FeeScheduleEntry>> GetCurrentScheduleAsync()
        {
            var today = _businessCalendar.Today();
            var entries = await _context.FeeSchedule.AsNoTracking()
                .Where(f => f.ValidFrom <= today && (f.ValidTo == null || f.ValidTo >= today))
                .ToListAsync();

            return entries
                .GroupBy(f => new { f.Code, f.Currency })
                .Select(g => g.OrderByDescending(f => f.ValidFrom).First())
                .OrderBy(f => f.AppliesTo)
                .ToList();
        }

        public async Task<FeeQuote> QuoteAsync(FeeAppliesTo appliesTo, decimal principal, string currency)
        {
            var schedule = await GetCachedScheduleAsync();
            var entry = schedule.FirstOrDefault(f => f.AppliesTo == appliesTo && f.Currency == currency);
            return entry == null ? new FeeQuote(0m, null) : new FeeQuote(entry.CalculateFor(principal), entry);
        }

        private async Task<IReadOnlyList<FeeScheduleEntry>> GetCachedScheduleAsync()
        {
            if (_cache == null)
                return await GetCurrentScheduleAsync();

            var key = $"fee-schedule:{_context.Database.GetDbConnection().Database}:{_businessCalendar.Today():yyyyMMdd}";
            return await _cache.GetOrCreateAsync(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ScheduleCacheDuration;
                return GetCurrentScheduleAsync();
            }) ?? [];
        }
    }
}
