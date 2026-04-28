using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Events;
using Task.API.Data;
using Task.API.Models;

namespace Task.API.BackgroundJobs
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

        public async System.Threading.Tasks.Task Execute()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            var now = DateTime.Now;
            var deadline = now.AddHours(24);

            var urgentTasks = await context.TaskItems
                .Where(t => t.DueDate != null && t.DueDate > now && t.DueDate <= deadline && t.Status != TaskItemStatus.Completed && t.AssignedTo != null)
                .ToListAsync();

            foreach (var task in urgentTasks)
            {
                await publishEndpoint.Publish(new TaskDeadlineEvent
                {
                    TaskId = task.TaskId,
                    TaskName = task.TaskName,
                    AssignedToUserId = task.AssignedTo!.Value,
                    DueDate = task.DueDate!.Value
                });
            }

            if (urgentTasks.Count > 0)
                _logger.LogInformation("Published {Count} deadline reminder events", urgentTasks.Count);
        }
    }

    public class SoftDeleteCleanupJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SoftDeleteCleanupJob> _logger;

        public SoftDeleteCleanupJob(IServiceScopeFactory scopeFactory, ILogger<SoftDeleteCleanupJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task Execute()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TaskDbContext>();

            var cutoff = DateTime.Now.AddDays(-30);
            var oldTasks = await context.TaskItems.IgnoreQueryFilters().Where(t => t.IsDeleted && t.DeletedAt < cutoff).ToListAsync();

            if (oldTasks.Count > 0)
            {
                context.TaskItems.RemoveRange(oldTasks);
                await context.SaveChangesAsync();
                _logger.LogInformation("Hard-deleted {Count} task records older than 30 days", oldTasks.Count);
            }
        }
    }
}
