using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly ApplicationDbContext _context;

        public AccountRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<Account?> GetByIdAsync(int id) =>
            _context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

        public Task<Account?> FindByNumberAsync(string accountNumber) =>
            _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);

        public async Task<IReadOnlyList<Account>> GetByUserIdAsync(int userId) =>
            await _context.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.CreatedAt)
                .ThenBy(a => a.Id)
                .ToListAsync();

        public Task<PagedResult<Account>> GetPagedAsync(string? search, PageRequest page)
        {
            var query = _context.Accounts.AsNoTracking().Include(a => a.User).AsQueryable();

            var pattern = search.ToLikePattern();
            if (pattern != null)
            {
                query = query.Where(a =>
                    EF.Functions.Like(a.AccountNumber, pattern) ||
                    EF.Functions.Like(a.User.FirstName, pattern) ||
                    EF.Functions.Like(a.User.LastName, pattern) ||
                    EF.Functions.Like(a.User.Email, pattern));
            }

            return query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .ToPagedResultAsync(page);
        }

        public async Task<IReadOnlyList<Account>> LockForUpdateAsync(IReadOnlyCollection<int> accountIds)
        {
            var orderedIds = accountIds.Distinct().OrderBy(id => id).ToArray();
            if (orderedIds.Length == 0)
                return [];

            foreach (var tracked in _context.ChangeTracker.Entries<Account>()
                         .Where(e => orderedIds.Contains(e.Entity.Id))
                         .ToList())
            {
                tracked.State = EntityState.Detached;
            }

            var placeholders = string.Join(", ", orderedIds.Select((_, index) => $"{{{index}}}"));
            var sql = $"SELECT * FROM accounts WHERE Id IN ({placeholders}) ORDER BY Id FOR UPDATE";

            return await _context.Accounts
                .FromSqlRaw(sql, orderedIds.Cast<object>().ToArray())
                .ToListAsync();
        }

        public Task<bool> AccountNumberExistsAsync(string accountNumber) =>
            _context.Accounts.AnyAsync(a => a.AccountNumber == accountNumber);

        public Task<bool> HasTransactionsAsync(int accountId) =>
            _context.Transactions.AnyAsync(t => t.FromAccountId == accountId || t.ToAccountId == accountId);

        public async Task AddAsync(Account account) => await _context.Accounts.AddAsync(account);

        public void Remove(Account account) => _context.Accounts.Remove(account);
    }
}
