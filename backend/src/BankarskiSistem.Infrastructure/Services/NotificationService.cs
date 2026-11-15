using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(INotificationRepository notificationRepository, IUnitOfWork unitOfWork)
        {
            _notificationRepository = notificationRepository;
            _unitOfWork = unitOfWork;
        }

        public Task<PagedResult<Notification>> GetPagedAsync(int userId, bool onlyUnread, PageRequest page) =>
            _notificationRepository.GetPagedAsync(userId, onlyUnread, page);

        public Task<int> CountUnreadAsync(int userId) =>
            _notificationRepository.CountUnreadAsync(userId);

        public async Task MarkAsReadAsync(int userId, int notificationId)
        {
            var notification = await _notificationRepository.GetAsync(userId, notificationId)
                ?? throw new NotFoundException("Obaveštenje nije pronađeno.");

            if (notification.IsRead)
                return;

            notification.IsRead = true;
            await _unitOfWork.SaveChangesAsync();
        }

        public Task<int> MarkAllAsReadAsync(int userId) =>
            _notificationRepository.MarkAllAsReadAsync(userId);
    }
}
