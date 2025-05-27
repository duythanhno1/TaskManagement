using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Managerment.Hubs
{
    public class TaskHub : Hub
    {
        private readonly ILogger<TaskHub> _logger;

        public TaskHub(ILogger<TaskHub> logger)
        {
            _logger = logger;
        }

        // This method can be called by clients to send a task update
        public async Task SendTaskUpdate(int taskId, string taskName, string description, int? assignedTo, string status)
        {
            try
            {
                if (string.IsNullOrEmpty(taskName) || string.IsNullOrEmpty(status))
                {
                    _logger.LogWarning("Invalid task update data: taskName or status is null or empty.");
                    return;
                }

                await Clients.All.SendAsync("ReceiveTaskUpdate", taskId, taskName, description, assignedTo, status);
                _logger.LogInformation("Task update sent successfully: {TaskId}", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending task update: {TaskId}", taskId);
            }
        }

        // You might want to add methods for specific updates, e.g., for assigned user only
        public async Task SendTaskAssignmentNotification(int userId, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogWarning("Invalid notification message: message is null or empty.");
                    return;
                }

                await Clients.User(userId.ToString()).SendAsync("ReceiveTaskAssignmentNotification", message);
                _logger.LogInformation("Task assignment notification sent to user: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending task assignment notification to user: {UserId}", userId);
            }
        }
    }
}
