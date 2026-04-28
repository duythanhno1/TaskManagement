using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Notification.API.Data;
using Notification.API.Hubs;
using Notification.API.Models;
using Shared.Contracts.Interfaces;
using Shared.Infrastructure;

namespace Notification.API.Services
{
    public interface INotificationService
    {
        Task<ServiceResult<List<object>>> GetMyNotificationsAsync(int userId, int page, int pageSize);
        Task<ServiceResult<object>> MarkAsReadAsync(int userId, int notificationId);
        Task<ServiceResult<object>> MarkAllAsReadAsync(int userId);
        Task CreateAndPushAsync(int userId, string type, string title, string content, string? referenceId = null);
        Task CreateBulkAndPushAsync(List<int> userIds, string type, string title, string content, string? referenceId = null);
    }

    public class NotificationService : INotificationService
    {
        private readonly NotificationDbContext _context;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILocalizer _localizer;

        public NotificationService(NotificationDbContext context, IHubContext<NotificationHub> hub, ILocalizer localizer)
        {
            _context = context; _hub = hub; _localizer = localizer;
        }

        public async Task<ServiceResult<List<object>>> GetMyNotificationsAsync(int userId, int page = 1, int pageSize = 20)
        {
            var notifications = await _context.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(n => new { n.NotificationId, n.Type, n.Title, n.Content, n.ReferenceId, n.IsRead, n.CreatedAt })
                .ToListAsync<object>();

            var unreadCount = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            return ServiceResult<List<object>>.Ok(notifications, $"UnreadCount:{unreadCount}");
        }

        public async Task<ServiceResult<object>> MarkAsReadAsync(int userId, int notificationId)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);
            if (notification == null) return ServiceResult<object>.NotFound(_localizer.Get("notification.not_found"));
            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return ServiceResult<object>.Ok(null, _localizer.Get("notification.marked_read"));
        }

        public async Task<ServiceResult<object>> MarkAllAsReadAsync(int userId)
        {
            var unread = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            await _context.SaveChangesAsync();
            return ServiceResult<object>.Ok(null, _localizer.Get("notification.all_marked", unread.Count));
        }

        public async Task CreateAndPushAsync(int userId, string type, string title, string content, string? referenceId = null)
        {
            var notification = new NotificationItem { UserId = userId, Type = type, Title = title, Content = content, ReferenceId = referenceId, CreatedAt = DateTime.Now };
            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();

            await _hub.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", new
            {
                notification.NotificationId, notification.Type, notification.Title, notification.Content, notification.ReferenceId, notification.CreatedAt
            });
        }

        public async Task CreateBulkAndPushAsync(List<int> userIds, string type, string title, string content, string? referenceId = null)
        {
            if (userIds.Count == 0) return;
            var notifications = userIds.Select(uid => new NotificationItem { UserId = uid, Type = type, Title = title, Content = content, ReferenceId = referenceId, CreatedAt = DateTime.Now }).ToList();
            await _context.Notifications.AddRangeAsync(notifications);
            await _context.SaveChangesAsync();

            var pushTasks = notifications.Select(n => _hub.Clients.User(n.UserId.ToString()).SendAsync("ReceiveNotification",
                new { n.NotificationId, n.Type, n.Title, n.Content, n.ReferenceId, n.CreatedAt }));
            await System.Threading.Tasks.Task.WhenAll(pushTasks);
        }
    }
}
