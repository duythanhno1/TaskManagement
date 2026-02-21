using Managerment.ApplicationContext;
using Managerment.DTO;
using Managerment.Hubs;
using Managerment.Interfaces;
using Managerment.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Managerment.Services
{
    public class TaskService : ITaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly IMemoryCache _cache;

        private const string AllTasksCacheKey = "AllTasks";
        private const string MyTasksPrefixCacheKey = "MyTasks_User_";
        private const string TaskByIdPrefixCacheKey = "TaskById_";

        private readonly MemoryCacheEntryOptions _cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(5))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

        public TaskService(ApplicationDbContext context, IHubContext<TaskHub> hubContext, IMemoryCache cache)
        {
            _context = context;
            _hubContext = hubContext;
            _cache = cache;
        }

        public async Task<ServiceResult<List<object>>> GetAllTasksAsync()
        {
            if (_cache.TryGetValue(AllTasksCacheKey, out List<object> cachedTasks))
            {
                return ServiceResult<List<object>>.Ok(cachedTasks, "Cache");
            }

            var tasks = await _context.TaskItems
                .Include(t => t.AssignedToUser)
                .Select(t => new
                {
                    t.TaskId,
                    t.TaskName,
                    t.Description,
                    AssignedToUserId = t.AssignedToUser != null ? (int?)t.AssignedToUser.UserId : null,
                    AssignedToUserName = t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
                    Status = t.Status.ToString(),
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .ToListAsync<object>();

            _cache.Set(AllTasksCacheKey, tasks, _cacheOptions);
            return ServiceResult<List<object>>.Ok(tasks, "Database");
        }

        public async Task<ServiceResult<List<object>>> GetMyTasksAsync(int userId)
        {
            string cacheKey = $"{MyTasksPrefixCacheKey}{userId}";

            if (_cache.TryGetValue(cacheKey, out List<object> cachedTasks))
            {
                return ServiceResult<List<object>>.Ok(cachedTasks, "Cache");
            }

            var tasks = await _context.TaskItems
                .Include(t => t.AssignedToUser)
                .Where(t => t.AssignedTo == userId)
                .Select(t => new
                {
                    t.TaskId,
                    t.TaskName,
                    t.Description,
                    AssignedToUserId = t.AssignedToUser != null ? (int?)t.AssignedToUser.UserId : null,
                    AssignedToUserName = t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
                    Status = t.Status.ToString(),
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .ToListAsync<object>();

            _cache.Set(cacheKey, tasks, _cacheOptions);
            return ServiceResult<List<object>>.Ok(tasks, "Database");
        }

        public async Task<ServiceResult<object>> GetTaskByIdAsync(int id)
        {
            string cacheKey = $"{TaskByIdPrefixCacheKey}{id}";

            if (_cache.TryGetValue(cacheKey, out object cachedTask))
            {
                return ServiceResult<object>.Ok(cachedTask, "Cache");
            }

            var task = await _context.TaskItems
                .Include(t => t.AssignedToUser)
                .Where(t => t.TaskId == id)
                .Select(t => new
                {
                    t.TaskId,
                    t.TaskName,
                    t.Description,
                    AssignedToUserId = t.AssignedToUser != null ? (int?)t.AssignedToUser.UserId : null,
                    AssignedToUserName = t.AssignedToUser != null ? t.AssignedToUser.FullName : null,
                    Status = t.Status.ToString(),
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (task == null)
            {
                return ServiceResult<object>.NotFound("Task not found.");
            }

            _cache.Set(cacheKey, task, _cacheOptions);
            return ServiceResult<object>.Ok(task, "Database");
        }

        public async Task<ServiceResult<object>> CreateTaskAsync(TaskCreateDTO dto)
        {
            var task = new TaskItem
            {
                TaskName = dto.TaskName,
                Description = dto.Description,
                AssignedTo = dto.AssignedTo,
                Status = Managerment.Model.TaskStatus.Todo,
                CreatedAt = DateTime.Now
            };

            _cache.Remove(AllTasksCacheKey);
            if (task.AssignedTo.HasValue)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{task.AssignedTo.Value}");
            }

            await _context.TaskItems.AddAsync(task);
            await _context.SaveChangesAsync();

            var assignedUser = dto.AssignedTo.HasValue
                ? await _context.Users.FindAsync(dto.AssignedTo.Value)
                : null;

            // SignalR notification
            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate",
                task.TaskId, task.TaskName, task.Description,
                task.AssignedTo, task.Status.ToString(),
                assignedUser?.FullName);

            if (assignedUser != null)
            {
                await _hubContext.Clients.User(assignedUser.UserId.ToString())
                    .SendAsync("ReceiveTaskAssignmentNotification",
                        $"You have been assigned a new task: {task.TaskName}");
            }

            return ServiceResult<object>.Created(
                new { task.TaskId },
                "Task created successfully");
        }

        public async Task<ServiceResult<object>> UpdateTaskAsync(int id, TaskUpdateDTO dto)
        {
            if (id != dto.TaskId)
            {
                return ServiceResult<object>.BadRequest("Task ID mismatch.");
            }

            var task = await _context.TaskItems.FindAsync(id);
            if (task == null)
            {
                return ServiceResult<object>.NotFound("Task not found.");
            }

            int? oldAssignedTo = task.AssignedTo;

            task.TaskName = dto.TaskName ?? task.TaskName;
            task.Description = dto.Description ?? task.Description;

            if (Enum.TryParse<Managerment.Model.TaskStatus>(dto.Status, true, out var newStatus))
            {
                task.Status = newStatus;
            }
            else
            {
                return ServiceResult<object>.BadRequest("Invalid status value.");
            }

            if (dto.AssignedTo.HasValue && dto.AssignedTo != task.AssignedTo)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{task.AssignedTo}");
                task.AssignedTo = dto.AssignedTo;
            }

            task.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Cache invalidation
            _cache.Remove(AllTasksCacheKey);
            _cache.Remove($"{TaskByIdPrefixCacheKey}{id}");
            if (oldAssignedTo.HasValue)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{oldAssignedTo.Value}");
            }
            if (task.AssignedTo.HasValue)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{task.AssignedTo.Value}");
            }

            // SignalR notification
            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate",
                task.TaskId, task.TaskName, task.Description,
                task.AssignedTo, task.Status.ToString());

            return ServiceResult<object>.Ok(null, "Task updated successfully.");
        }

        public async Task<ServiceResult<object>> AssignTaskAsync(TaskAssignDTO dto)
        {
            var task = await _context.TaskItems.FindAsync(dto.TaskId);
            if (task == null)
            {
                return ServiceResult<object>.NotFound("Task not found.");
            }

            var newAssignee = await _context.Users.FindAsync(dto.NewAssignedToUserId);
            if (newAssignee == null)
            {
                return ServiceResult<object>.BadRequest("New assigned user not found.");
            }

            int? oldAssignedTo = task.AssignedTo;
            task.AssignedTo = dto.NewAssignedToUserId;
            task.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Cache invalidation
            _cache.Remove(AllTasksCacheKey);
            _cache.Remove($"{TaskByIdPrefixCacheKey}{dto.TaskId}");
            if (oldAssignedTo.HasValue)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{oldAssignedTo.Value}");
            }
            _cache.Remove($"{MyTasksPrefixCacheKey}{dto.NewAssignedToUserId}");

            // SignalR notification
            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate",
                task.TaskId, task.TaskName, task.Description,
                task.AssignedTo, task.Status.ToString());

            await _hubContext.Clients.User(newAssignee.UserId.ToString())
                .SendAsync("ReceiveTaskAssignmentNotification",
                    $"You have been assigned to task: {task.TaskName}");

            return ServiceResult<object>.Ok(null,
                $"Task assigned to {newAssignee.FullName} successfully.");
        }

        public async Task<ServiceResult<object>> DeleteTaskAsync(int id)
        {
            var task = await _context.TaskItems
                .Include(t => t.AssignedToUser)
                .FirstOrDefaultAsync(t => t.TaskId == id);

            if (task == null)
            {
                return ServiceResult<object>.NotFound("Task not found.");
            }

            int? assignedToUser = task.AssignedTo;

            // Cache invalidation
            _cache.Remove(AllTasksCacheKey);
            _cache.Remove($"{TaskByIdPrefixCacheKey}{id}");
            if (assignedToUser.HasValue)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{assignedToUser.Value}");
            }

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();

            // SignalR notification
            await _hubContext.Clients.All.SendAsync("ReceiveTaskDelete", id);

            return ServiceResult<object>.Ok(null, "Task deleted successfully.");
        }

        public async Task<ServiceResult<List<object>>> GetAllUsersAsync()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email
                })
                .ToListAsync<object>();

            return ServiceResult<List<object>>.Ok(users);
        }
    }
}
