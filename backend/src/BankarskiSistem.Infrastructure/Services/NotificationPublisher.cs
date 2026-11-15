using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Services
{
    public class NotificationPublisher : INotificationPublisher
    {
        private readonly ApplicationDbContext _context;

        public NotificationPublisher(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task PublishAsync(params Notification[] notifications)
        {
            if (notifications.Length == 0)
                return;

            var userIds = notifications.Select(n => n.UserId).Distinct().ToList();
            var preferences = await _context.UserPreferences.AsNoTracking()
                .Where(p => userIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            var allowed = notifications
                .Where(n => !preferences.TryGetValue(n.UserId, out var userPreferences) || userPreferences.Allows(n.Type))
                .ToList();

            await _context.Notifications.AddRangeAsync(allowed);
        }
    }
}
