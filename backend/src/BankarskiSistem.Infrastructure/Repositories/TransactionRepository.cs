using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly ApplicationDbContext _context;

        public TransactionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        private IQueryable<Transaction> WithDetails() =>
            _context.Transactions
                .AsNoTracking()
                .Include(t => t.FromAccount)
                .Include(t => t.ToAccount)
                .Include(t => t.PerformedBy)
                .Include(t => t.PaymentOrder);

        public Task<Transaction?> GetByIdAsync(int id) =>
            WithDetails().FirstOrDefaultAsync(t => t.Id == id);

        public Task<Transaction?> GetByReferenceAsync(string referenceNumber) =>
            WithDetails().FirstOrDefaultAsync(t => t.ReferenceNumber == referenceNumber);

        public Task<Transaction?> GetByIdempotencyKeyAsync(int userId, string idempotencyKey) =>
            WithDetails().FirstOrDefaultAsync(t =>
                t.PerformedByUserId == userId && t.IdempotencyKey == idempotencyKey);

        public async Task<PagedResult<Transaction>> GetPagedAsync(TransactionFilter filter, PageRequest page) =>
            await ApplyFilter(WithDetails(), filter, await OwnerAccountIdsAsync(filter))
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .ToPagedResultAsync(page);

        private async Task<List<int>?> OwnerAccountIdsAsync(TransactionFilter filter) =>
            filter.OwnerUserId.HasValue
                ? await _context.Accounts.AsNoTracking().Where(a => a.UserId == filter.OwnerUserId.Value).Select(a => a.Id).ToListAsync()
                : null;

        public async Task<CursorPage<Transaction>> GetCursorPageAsync(TransactionFilter filter, string? cursor, int limit)
        {
            var query = ApplyFilter(WithDetails(), filter, await OwnerAccountIdsAsync(filter));

            if (TransactionCursor.TryDecode(cursor, out var createdAt, out var id))
                query = query.Where(t => t.CreatedAt < createdAt || (t.CreatedAt == createdAt && t.Id < id));

            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Take(limit + 1)
                .ToListAsync();

            var hasMore = items.Count > limit;
            if (hasMore)
                items.RemoveAt(items.Count - 1);

            var nextCursor = hasMore ? TransactionCursor.Encode(items[^1].CreatedAt, items[^1].Id) : null;
            return new CursorPage<Transaction>(items, nextCursor, hasMore);
        }

        private static IQueryable<Transaction> ApplyFilter(IQueryable<Transaction> query, TransactionFilter filter, List<int>? ownerAccountIds)
        {

            if (ownerAccountIds != null)
            {
                query = query.Where(t =>
                    (t.FromAccountId != null && ownerAccountIds.Contains(t.FromAccountId.Value)) ||
                    (t.ToAccountId != null && ownerAccountIds.Contains(t.ToAccountId.Value)));
            }

            if (filter.AccountId.HasValue)
            {
                var accountId = filter.AccountId.Value;
                query = query.Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId);
            }

            if (filter.Type.HasValue)
                query = query.Where(t => t.Type == filter.Type.Value);

            if (filter.Status.HasValue)
                query = query.Where(t => t.Status == filter.Status.Value);

            if (filter.FromUtc.HasValue)
                query = query.Where(t => t.CreatedAt >= filter.FromUtc.Value);

            if (filter.ToUtc.HasValue)
                query = query.Where(t => t.CreatedAt < filter.ToUtc.Value);

            var pattern = filter.Search.ToLikePattern();
            if (pattern != null)
            {
                query = query.Where(t =>
                    EF.Functions.Like(t.ReferenceNumber, pattern) ||
                    (t.Description != null && EF.Functions.Like(t.Description, pattern)) ||
                    (t.FromAccount != null && EF.Functions.Like(t.FromAccount.AccountNumber, pattern)) ||
                    (t.ToAccount != null && EF.Functions.Like(t.ToAccount.AccountNumber, pattern)) ||
                    (t.PaymentOrder != null && EF.Functions.Like(t.PaymentOrder.RecipientName, pattern)));
            }

            return query;
        }

        public async Task<IReadOnlyList<Transaction>> GetRecentForUserAsync(int userId, int take)
        {
            var accountIds = await _context.Accounts.AsNoTracking().Where(a => a.UserId == userId).Select(a => a.Id).ToListAsync();
            return await WithDetails()
                .Where(t =>
                    (t.FromAccountId != null && accountIds.Contains(t.FromAccountId.Value)) ||
                    (t.ToAccountId != null && accountIds.Contains(t.ToAccountId.Value)))
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Take(take)
                .ToListAsync();
        }

        public async Task<(decimal Total, int Count)> GetOutgoingSinceAsync(int accountId, DateTime sinceUtc)
        {
            var summary = await _context.Transactions
                .Where(t => t.FromAccountId == accountId
                            && t.Type != TransactionType.Konverzija
                            && t.Status == TransactionStatus.Uspesna
                            && t.CreatedAt >= sinceUtc)
                .GroupBy(_ => 1)
                .Select(g => new { Total = g.Sum(t => t.Amount), Count = g.Count() })
                .FirstOrDefaultAsync();

            return summary == null ? (0m, 0) : (summary.Total, summary.Count);
        }

        public async Task AddAsync(Transaction transaction) =>
            await _context.Transactions.AddAsync(transaction);
    }
}
