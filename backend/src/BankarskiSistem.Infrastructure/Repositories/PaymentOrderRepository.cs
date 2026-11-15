using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Repositories
{
    public class PaymentOrderRepository : IPaymentOrderRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentOrderRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        private IQueryable<PaymentOrder> WithDetails() =>
            _context.PaymentOrders
                .Include(p => p.FromAccount)
                .Include(p => p.Transaction);

        public Task<PaymentOrder?> GetForUserAsync(int userId, int orderId) =>
            WithDetails().FirstOrDefaultAsync(p => p.Id == orderId && p.UserId == userId);

        public Task<PaymentOrder?> GetByIdempotencyKeyAsync(int userId, string idempotencyKey) =>
            WithDetails().AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.IdempotencyKey == idempotencyKey);

        public Task<PagedResult<PaymentOrder>> GetPagedForUserAsync(int userId, PaymentOrderFilter filter, PageRequest page)
        {
            var query = WithDetails().AsNoTracking().Where(p => p.UserId == userId);

            if (filter.Status.HasValue)
                query = query.Where(p => p.Status == filter.Status.Value);

            if (filter.AccountId.HasValue)
                query = query.Where(p => p.FromAccountId == filter.AccountId.Value);

            return query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .ToPagedResultAsync(page);
        }

        public async Task<IReadOnlyList<int>> GetDueOrderIdsAsync(DateOnly businessDate, int take) =>
            await _context.PaymentOrders
                .AsNoTracking()
                .Where(p => p.Status == PaymentOrderStatus.Zakazan && p.ScheduledFor <= businessDate)
                .OrderBy(p => p.ScheduledFor)
                .ThenBy(p => p.Id)
                .Select(p => p.Id)
                .Take(take)
                .ToListAsync();

        public async Task<PaymentOrder?> TryLockDueOrderAsync(int orderId)
        {
            foreach (var tracked in _context.ChangeTracker.Entries<PaymentOrder>().Where(e => e.Entity.Id == orderId).ToList())
                tracked.State = EntityState.Detached;

            var locked = await _context.PaymentOrders
                .FromSqlInterpolated($"SELECT * FROM payment_orders WHERE Id = {orderId} AND Status = 'Zakazan' FOR UPDATE SKIP LOCKED")
                .ToListAsync();

            return locked.SingleOrDefault();
        }

        public async Task AddAsync(PaymentOrder order) => await _context.PaymentOrders.AddAsync(order);
    }

    public class BeneficiaryRepository : IBeneficiaryRepository
    {
        private readonly ApplicationDbContext _context;

        public BeneficiaryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Beneficiary>> GetForUserAsync(int userId) =>
            await _context.Beneficiaries
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.LastUsedAt ?? b.CreatedAt)
                .ThenBy(b => b.Nickname)
                .ToListAsync();

        public Task<Beneficiary?> GetAsync(int userId, int beneficiaryId) =>
            _context.Beneficiaries.FirstOrDefaultAsync(b => b.Id == beneficiaryId && b.UserId == userId);

        public Task<Beneficiary?> FindByNicknameAsync(int userId, string nickname) =>
            _context.Beneficiaries.FirstOrDefaultAsync(b => b.UserId == userId && b.Nickname == nickname);

        public async Task AddAsync(Beneficiary beneficiary) => await _context.Beneficiaries.AddAsync(beneficiary);

        public void Remove(Beneficiary beneficiary) => _context.Beneficiaries.Remove(beneficiary);
    }

    public class LegalRepository : ILegalRepository
    {
        private readonly ApplicationDbContext _context;

        public LegalRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        private IQueryable<LegalDocument> CurrentDocuments() =>
            _context.LegalDocuments
                .AsNoTracking()
                .Where(d => d.PublishedAt == _context.LegalDocuments
                    .Where(other => other.DocumentType == d.DocumentType)
                    .Max(other => other.PublishedAt));

        public async Task<IReadOnlyList<LegalDocument>> GetCurrentDocumentsAsync() =>
            await CurrentDocuments().OrderBy(d => d.DocumentType).ToListAsync();

        public async Task<IReadOnlyList<LegalDocument>> GetPendingForUserAsync(int userId) =>
            await CurrentDocuments()
                .Where(d => !_context.UserConsents.Any(c => c.UserId == userId && c.LegalDocumentId == d.Id))
                .OrderBy(d => d.DocumentType)
                .ToListAsync();

        public async Task<IReadOnlyList<UserConsent>> GetConsentsForUserAsync(int userId) =>
            await _context.UserConsents
                .AsNoTracking()
                .Include(c => c.LegalDocument)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.AcceptedAt)
                .ToListAsync();

        public Task<bool> VersionExistsAsync(LegalDocumentType type, string version) =>
            _context.LegalDocuments.AnyAsync(d => d.DocumentType == type && d.Version == version);

        public async Task AddConsentAsync(UserConsent consent) => await _context.UserConsents.AddAsync(consent);

        public async Task AddDocumentAsync(LegalDocument document) => await _context.LegalDocuments.AddAsync(document);
    }
}
