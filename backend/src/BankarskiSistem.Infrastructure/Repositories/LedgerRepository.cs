using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BankarskiSistem.Infrastructure.Repositories
{
    public class LedgerRepository : ILedgerRepository
    {
        private static readonly LedgerAccountType[] SystemAccountTypes =
        [
            LedgerAccountType.Blagajna,
            LedgerAccountType.Uskladjivanje,
            LedgerAccountType.Menjacnica,
            LedgerAccountType.Provizije,
            LedgerAccountType.Kliring
        ];

        private static readonly MemoryCache SystemAccountIds = new(new MemoryCacheOptions());

        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;

        public LedgerRepository(ApplicationDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyDictionary<int, int>> GetCustomerLedgerAccountIdsAsync(IReadOnlyCollection<int> accountIds)
        {
            var mapping = await _context.LedgerAccounts
                .AsNoTracking()
                .Where(l => l.AccountId != null && accountIds.Contains(l.AccountId.Value))
                .Select(l => new { AccountId = l.AccountId!.Value, l.Id })
                .ToDictionaryAsync(l => l.AccountId, l => l.Id);

            var missing = accountIds.Except(mapping.Keys).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"Racun(i) {string.Join(", ", missing)} nemaju konto u glavnoj knjizi.");

            return mapping;
        }

        public async Task<int> GetSystemLedgerAccountIdAsync(LedgerAccountType type, string currency)
        {
            var code = $"{LedgerAccount.SystemCodePrefix(type)}-{currency}";
            var cacheKey = $"{_context.Database.GetDbConnection().Database}:{code}";
            if (SystemAccountIds.TryGetValue(cacheKey, out int cachedId))
                return cachedId;

            var id = await _context.LedgerAccounts
                .AsNoTracking()
                .Where(l => l.Code == code)
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync();

            if (id == null)
                throw new InvalidOperationException($"Sistemsko konto {code} ne postoji.");

            SystemAccountIds.Set(cacheKey, id.Value);
            return id.Value;
        }

        public async Task EnsureSystemAccountsAsync(string currency)
        {
            var codes = SystemAccountTypes.Select(t => $"{LedgerAccount.SystemCodePrefix(t)}-{currency}").ToList();
            var existing = await _context.LedgerAccounts
                .Where(l => codes.Contains(l.Code))
                .Select(l => l.Code)
                .ToListAsync();

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var type in SystemAccountTypes)
            {
                var ledgerAccount = LedgerAccount.ForSystem(type, currency, now);
                if (!existing.Contains(ledgerAccount.Code))
                    await _context.LedgerAccounts.AddAsync(ledgerAccount);
            }
        }

        public async Task AddLedgerAccountAsync(LedgerAccount ledgerAccount) =>
            await _context.LedgerAccounts.AddAsync(ledgerAccount);

        public async Task AddJournalEntryAsync(JournalEntry entry) =>
            await _context.JournalEntries.AddAsync(entry);
    }
}
