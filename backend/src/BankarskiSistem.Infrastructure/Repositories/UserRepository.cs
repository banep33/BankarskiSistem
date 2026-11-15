using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<User?> GetByIdAsync(int id) =>
            _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        public Task<User?> GetByEmailAsync(string normalizedEmail) =>
            _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        public Task<bool> EmailExistsAsync(string normalizedEmail, int? excludeUserId = null) =>
            _context.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != excludeUserId);

        public Task<bool> AnyAdministratorAsync() =>
            _context.Users.AnyAsync(u => u.Role == UserRole.Administrator);

        public async Task<bool> HasFinancialHistoryAsync(int userId) =>
            await _context.Accounts.AnyAsync(a => a.UserId == userId)
            || await _context.Transactions.AnyAsync(t => t.PerformedByUserId == userId);

        public Task<PagedResult<User>> GetPagedAsync(UserFilter filter, PageRequest page)
        {
            var query = _context.Users.AsNoTracking();

            if (filter.Role.HasValue)
                query = query.Where(u => u.Role == filter.Role.Value);

            if (filter.IsActive.HasValue)
                query = query.Where(u => u.IsActive == filter.IsActive.Value);

            var pattern = filter.Search.ToLikePattern();
            if (pattern != null)
            {
                query = query.Where(u =>
                    EF.Functions.Like(u.FirstName, pattern) ||
                    EF.Functions.Like(u.LastName, pattern) ||
                    EF.Functions.Like(u.Email, pattern));
            }

            return query
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ThenBy(u => u.Id)
                .ToPagedResultAsync(page);
        }

        public async Task AddAsync(User user) => await _context.Users.AddAsync(user);

        public void Remove(User user) => _context.Users.Remove(user);
    }
}
