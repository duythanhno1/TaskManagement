using Managerment.ApplicationContext;
using Managerment.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Managerment.BackgroundJobs
{
    public class SoftDeleteCleanupJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SoftDeleteCleanupJob> _logger;

        public SoftDeleteCleanupJob(IServiceScopeFactory scopeFactory, ILogger<SoftDeleteCleanupJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task Execute()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var cutoff = DateTime.Now.AddDays(-30);
            int totalDeleted = 0;

            // Hard-delete TaskItems soft-deleted > 30 days
            var oldTasks = await context.TaskItems
                .IgnoreQueryFilters()
                .Where(t => t.IsDeleted && t.DeletedAt < cutoff)
                .ToListAsync();
            if (oldTasks.Count > 0)
            {
                context.TaskItems.RemoveRange(oldTasks);
                totalDeleted += oldTasks.Count;
            }

            // Hard-delete ChatMessages soft-deleted > 30 days
            var oldMessages = await context.ChatMessages
                .IgnoreQueryFilters()
                .Where(m => m.IsDeleted && m.DeletedAt < cutoff)
                .ToListAsync();
            if (oldMessages.Count > 0)
            {
                context.ChatMessages.RemoveRange(oldMessages);
                totalDeleted += oldMessages.Count;
            }

            // Hard-delete ChatGroups soft-deleted > 30 days
            var oldGroups = await context.ChatGroups
                .IgnoreQueryFilters()
                .Where(g => g.IsDeleted && g.DeletedAt < cutoff)
                .ToListAsync();
            if (oldGroups.Count > 0)
            {
                context.ChatGroups.RemoveRange(oldGroups);
                totalDeleted += oldGroups.Count;
            }

            if (totalDeleted > 0)
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("Hard-deleted {Count} soft-deleted records older than 30 days", totalDeleted);
            }
        }
    }
}
