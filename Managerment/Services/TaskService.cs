using System.Text.Json;
using Managerment.ApplicationContext;
using Managerment.DTO;
using Managerment.Hubs;
using Managerment.Interfaces;
using Managerment.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Managerment.Services
{
    public class TaskService : ITaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly IDistributedCache _cache;
        private readonly ILogger<TaskService> _logger;

        private const string AllTasksCacheKey = "AllTasks";
        private const string MyTasksPrefixCacheKey = "MyTasks_User_";
        private const string TaskByIdPrefixCacheKey = "TaskById_";

        private readonly DistributedCacheEntryOptions _cacheOptions;

        public TaskService(
            ApplicationDbContext context,
            IHubContext<TaskHub> hubContext,
            IDistributedCache cache,
            IConfiguration configuration,
            ILogger<TaskService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _cache = cache;
            _logger = logger;

            var slidingMinutes = configuration.GetValue("Cache:SlidingExpirationMinutes", 5);
            var absoluteMinutes = configuration.GetValue("Cache:AbsoluteExpirationMinutes", 30);

            _cacheOptions = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(slidingMinutes),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(absoluteMinutes)
            };
        }

        // Helper: Get or set distributed cache
        private async Task<T?> GetFromCacheAsync<T>(string key)
        {
            var cached = await _cache.GetStringAsync(key);
            if (cached != null)
            {
                _logger.LogDebug("Cache HIT for key: {CacheKey}", key);
                return JsonSerializer.Deserialize<T>(cached);
            }
            return default;
        }

        private async Task SetCacheAsync<T>(string key, T data)
        {
            var json = JsonSerializer.Serialize(data);
            await _cache.SetStringAsync(key, json, _cacheOptions);
            _logger.LogDebug("Cache SET for key: {CacheKey}", key);
        }

        private async Task InvalidateCacheAsync(params string[] keys)
        {
            foreach (var key in keys)
            {
                await _cache.RemoveAsync(key);
            }
            _logger.LogDebug("Cache INVALIDATED for keys: {CacheKeys}", string.Join(", ", keys));
        }

        public async Task<ServiceResult<List<object>>> GetAllTasksAsync()
        {
            var cached = await _cache.GetStringAsync(AllTasksCacheKey);
            if (cached != null)
            {
                _logger.LogDebug("Cache HIT for key: {CacheKey}", AllTasksCacheKey);
                var cachedData = JsonSerializer.Deserialize<List<object>>(cached);
                return ServiceResult<List<object>>.Ok(cachedData ?? new List<object>(), "Cache");
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

            await SetCacheAsync(AllTasksCacheKey, tasks);
            return ServiceResult<List<object>>.Ok(tasks, "Database");
        }

        public async Task<ServiceResult<List<object>>> GetMyTasksAsync(int userId)
        {
            string cacheKey = $"{MyTasksPrefixCacheKey}{userId}";

            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("Cache HIT for key: {CacheKey}", cacheKey);
                var cachedData = JsonSerializer.Deserialize<List<object>>(cached);
                return ServiceResult<List<object>>.Ok(cachedData ?? new List<object>(), "Cache");
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

            await SetCacheAsync(cacheKey, tasks);
            return ServiceResult<List<object>>.Ok(tasks, "Database");
        }

        public async Task<ServiceResult<object>> GetTaskByIdAsync(int id)
        {
            string cacheKey = $"{TaskByIdPrefixCacheKey}{id}";

            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                _logger.LogDebug("Cache HIT for key: {CacheKey}", cacheKey);
                var cachedData = JsonSerializer.Deserialize<object>(cached);
                return ServiceResult<object>.Ok(cachedData!, "Cache");
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

            await SetCacheAsync(cacheKey, task);
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

            // Cache invalidation
            var keysToInvalidate = new List<string> { AllTasksCacheKey };
            if (task.AssignedTo.HasValue)
            {
                keysToInvalidate.Add($"{MyTasksPrefixCacheKey}{task.AssignedTo.Value}");
            }
            await InvalidateCacheAsync(keysToInvalidate.ToArray());

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

            _logger.LogInformation("Task {TaskId} created by user", task.TaskId);

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
                task.AssignedTo = dto.AssignedTo;
            }

            task.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Cache invalidation
            var keysToInvalidate = new List<string>
            {
                AllTasksCacheKey,
                $"{TaskByIdPrefixCacheKey}{id}"
            };
            if (oldAssignedTo.HasValue) keysToInvalidate.Add($"{MyTasksPrefixCacheKey}{oldAssignedTo.Value}");
            if (task.AssignedTo.HasValue) keysToInvalidate.Add($"{MyTasksPrefixCacheKey}{task.AssignedTo.Value}");
            await InvalidateCacheAsync(keysToInvalidate.ToArray());

            // SignalR notification
            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate",
                task.TaskId, task.TaskName, task.Description,
                task.AssignedTo, task.Status.ToString());

            _logger.LogInformation("Task {TaskId} updated", task.TaskId);

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
            var keysToInvalidate = new List<string>
            {
                AllTasksCacheKey,
                $"{TaskByIdPrefixCacheKey}{dto.TaskId}",
                $"{MyTasksPrefixCacheKey}{dto.NewAssignedToUserId}"
            };
            if (oldAssignedTo.HasValue) keysToInvalidate.Add($"{MyTasksPrefixCacheKey}{oldAssignedTo.Value}");
            await InvalidateCacheAsync(keysToInvalidate.ToArray());

            // SignalR notification
            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate",
                task.TaskId, task.TaskName, task.Description,
                task.AssignedTo, task.Status.ToString());

            await _hubContext.Clients.User(newAssignee.UserId.ToString())
                .SendAsync("ReceiveTaskAssignmentNotification",
                    $"You have been assigned to task: {task.TaskName}");

            _logger.LogInformation("Task {TaskId} assigned to user {UserId}", dto.TaskId, dto.NewAssignedToUserId);

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
            var keysToInvalidate = new List<string>
            {
                AllTasksCacheKey,
                $"{TaskByIdPrefixCacheKey}{id}"
            };
            if (assignedToUser.HasValue) keysToInvalidate.Add($"{MyTasksPrefixCacheKey}{assignedToUser.Value}");
            await InvalidateCacheAsync(keysToInvalidate.ToArray());

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();

            // SignalR notification
            await _hubContext.Clients.All.SendAsync("ReceiveTaskDelete", id);

            _logger.LogInformation("Task {TaskId} deleted", id);

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
