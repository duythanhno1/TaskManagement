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

        public NotificationService(ApplicationDbContext context, IHubContext<ChatHub> chatHubContext)
        {
            _context = context;
            _chatHubContext = chatHubContext;
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
                return ServiceResult<object>.NotFound("Notification not found.");
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return ServiceResult<object>.Ok(null, "Notification marked as read.");
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

            return ServiceResult<object>.Ok(null, $"{unreadNotifications.Count} notifications marked as read.");
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

            // Push real-time notification
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

            // Push real-time notifications in parallel
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
