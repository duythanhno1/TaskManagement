using Managerment.ApplicationContext;
using Managerment.Hubs;
using Managerment.Interfaces;
using Managerment.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Managerment.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly ILocalizer _localizer;

        public NotificationService(ApplicationDbContext context, IHubContext<ChatHub> chatHubContext, ILocalizer localizer)
        {
            _context = context;
            _chatHubContext = chatHubContext;
            _localizer = localizer;
        }

        public async Task<ServiceResult<List<object>>> GetMyNotificationsAsync(int userId, int page = 1, int pageSize = 20)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new
                {
                    n.NotificationId,
                    n.Type,
                    n.Title,
                    n.Content,
                    n.ReferenceId,
                    n.IsRead,
                    n.CreatedAt
                })
                .ToListAsync<object>();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return ServiceResult<List<object>>.Ok(notifications, $"UnreadCount:{unreadCount}");
        }

        public async Task<ServiceResult<object>> MarkAsReadAsync(int userId, int notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return ServiceResult<object>.NotFound(_localizer.Get("notification.not_found"));
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return ServiceResult<object>.Ok(null, _localizer.Get("notification.marked_read"));
        }

        public async Task<ServiceResult<object>> MarkAllAsReadAsync(int userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return ServiceResult<object>.Ok(null, _localizer.Get("notification.all_marked", unreadNotifications.Count));
        }

        public async Task CreateNotificationAsync(int userId, string type, string title, string content, string referenceId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Content = content,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();

            await _chatHubContext.Clients.User(userId.ToString())
                .SendAsync("ReceiveNotification", new
                {
                    notification.NotificationId,
                    notification.Type,
                    notification.Title,
                    notification.Content,
                    notification.ReferenceId,
                    notification.CreatedAt
                });
        }

        public async Task CreateBulkNotificationsAsync(List<int> userIds, string type, string title, string content, string referenceId = null)
        {
            if (userIds == null || userIds.Count == 0) return;

            var notifications = userIds.Select(userId => new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Content = content,
                ReferenceId = referenceId,
                IsRead = false,
                CreatedAt = DateTime.Now
            }).ToList();

            await _context.Notifications.AddRangeAsync(notifications);
            await _context.SaveChangesAsync();

            var pushTasks = notifications.Select(n =>
                _chatHubContext.Clients.User(n.UserId.ToString())
                    .SendAsync("ReceiveNotification", new
                    {
                        n.NotificationId,
                        n.Type,
                        n.Title,
                        n.Content,
                        n.ReferenceId,
                        n.CreatedAt
                    }));

            await Task.WhenAll(pushTasks);
        }
    }
}
