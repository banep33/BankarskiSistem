using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly ApplicationDbContext _context;

        public AuditLogRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task AddAsync(AuditLog auditLog) =>
            _context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO auditlogs
                    (UserId, Action, EntityType, EntityId, IpAddress, OldValue, NewValue, IsSuccess, ErrorMessage, CreatedAt)
                VALUES
                    ({auditLog.UserId}, {auditLog.Action}, {auditLog.EntityType}, {auditLog.EntityId},
                     {auditLog.IpAddress}, {auditLog.OldValue}, {auditLog.NewValue}, {auditLog.IsSuccess},
                     {auditLog.ErrorMessage}, {auditLog.CreatedAt})");

        public Task<PagedResult<AuditLog>> GetPagedAsync(AuditLogFilter filter, PageRequest page)
        {
            var query = _context.AuditLogs.AsNoTracking().Include(a => a.User).AsQueryable();

            if (filter.UserId.HasValue)
                query = query.Where(a => a.UserId == filter.UserId.Value);

            if (filter.OnlyFailures == true)
                query = query.Where(a => !a.IsSuccess);

            if (filter.FromUtc.HasValue)
                query = query.Where(a => a.CreatedAt >= filter.FromUtc.Value);

            if (filter.ToUtc.HasValue)
                query = query.Where(a => a.CreatedAt < filter.ToUtc.Value);

            var pattern = filter.Search.ToLikePattern();
            if (pattern != null)
            {
                query = query.Where(a =>
                    EF.Functions.Like(a.Action, pattern) ||
                    (a.NewValue != null && EF.Functions.Like(a.NewValue, pattern)) ||
                    (a.IpAddress != null && EF.Functions.Like(a.IpAddress, pattern)) ||
                    (a.User != null && EF.Functions.Like(a.User.Email, pattern)));
            }

            return query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .ToPagedResultAsync(page);
        }
    }
}
