using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly ApplicationDbContext _context;

        public NotificationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<PagedResult<Notification>> GetPagedAsync(int userId, bool onlyUnread, PageRequest page)
        {
            var query = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);

            if (onlyUnread)
                query = query.Where(n => !n.IsRead);

            return query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
                .ToPagedResultAsync(page);
        }

        public Task<int> CountUnreadAsync(int userId) =>
            _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public Task<Notification?> GetAsync(int userId, int notificationId) =>
            _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        public Task<int> MarkAllAsReadAsync(int userId) =>
            _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));

        public Task AddRangeAsync(IEnumerable<Notification> notifications) =>
            _context.Notifications.AddRangeAsync(notifications);
    }
}
