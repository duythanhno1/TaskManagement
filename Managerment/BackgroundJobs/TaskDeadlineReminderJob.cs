using Managerment.ApplicationContext;
using Managerment.Model;
using Microsoft.EntityFrameworkCore;

namespace Managerment.BackgroundJobs
{
    public class TaskDeadlineReminderJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TaskDeadlineReminderJob> _logger;

        public TaskDeadlineReminderJob(IServiceScopeFactory scopeFactory, ILogger<TaskDeadlineReminderJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task Execute()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = DateTime.Now;
            var deadline = now.AddHours(24);

            // Find tasks with DueDate within next 24h, not completed, assigned to someone
            var urgentTasks = await context.TaskItems
                .Where(t => t.DueDate != null
                    && t.DueDate > now
                    && t.DueDate <= deadline
                    && t.Status != Model.TaskStatus.Completed
                    && t.AssignedTo != null)
                .ToListAsync();

            if (urgentTasks.Count == 0) return;

            var notifications = urgentTasks.Select(task => new Notification
            {
                UserId = task.AssignedTo!.Value,
                Type = "TaskDeadline",
                Title = "Task sắp hết hạn",
                Content = $"Task \"{task.TaskName}\" sẽ hết hạn lúc {task.DueDate:dd/MM/yyyy HH:mm}",
                ReferenceId = task.TaskId.ToString(),
                IsRead = false,
                CreatedAt = DateTime.Now
            }).ToList();

            await context.Notifications.AddRangeAsync(notifications);
            await context.SaveChangesAsync();

            _logger.LogInformation("Sent {Count} deadline reminder notifications", notifications.Count);
        }
    }
}
