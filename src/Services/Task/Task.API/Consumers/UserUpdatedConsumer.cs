using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Events;
using Task.API.Data;

namespace Task.API.Consumers
{
    public class UserUpdatedConsumer : IConsumer<UserUpdatedEvent>
    {
        private readonly TaskDbContext _context;
        private readonly ILogger<UserUpdatedConsumer> _logger;

        public UserUpdatedConsumer(TaskDbContext context, ILogger<UserUpdatedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async System.Threading.Tasks.Task Consume(ConsumeContext<UserUpdatedEvent> context)
        {
            var evt = context.Message;
            var tasks = await _context.TaskItems.Where(t => t.AssignedTo == evt.UserId).ToListAsync();
            foreach (var task in tasks)
            {
                task.AssignedToUserName = evt.FullName;
            }
            if (tasks.Count > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated denormalized user name for {Count} tasks (UserId: {UserId})", tasks.Count, evt.UserId);
            }
        }
    }
}
