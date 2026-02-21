using Managerment.Services;

namespace Managerment.Interfaces
{
    public interface INotificationService
    {
        Task<ServiceResult<List<object>>> GetMyNotificationsAsync(int userId, int page = 1, int pageSize = 20);
        Task<ServiceResult<object>> MarkAsReadAsync(int userId, int notificationId);
        Task<ServiceResult<object>> MarkAllAsReadAsync(int userId);
        Task CreateNotificationAsync(int userId, string type, string title, string content, string referenceId = null);
        Task CreateBulkNotificationsAsync(List<int> userIds, string type, string title, string content, string referenceId = null);
    }
}
