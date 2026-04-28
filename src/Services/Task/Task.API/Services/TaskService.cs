using System.Text.Json;
using Auth.API.Protos;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;
using Shared.Infrastructure;
using Task.API.Data;
using Task.API.Hubs;
using Task.API.Models;
using Microsoft.AspNetCore.SignalR;

namespace Task.API.Services
{
    public interface ITaskService
    {
        System.Threading.Tasks.Task<ServiceResult<List<object>>> GetAllTasksAsync();
        System.Threading.Tasks.Task<ServiceResult<List<object>>> GetMyTasksAsync(int userId);
        System.Threading.Tasks.Task<ServiceResult<object>> GetTaskByIdAsync(int id);
        System.Threading.Tasks.Task<ServiceResult<object>> CreateTaskAsync(TaskCreateDTO dto);
        System.Threading.Tasks.Task<ServiceResult<object>> UpdateTaskAsync(int id, TaskUpdateDTO dto);
        System.Threading.Tasks.Task<ServiceResult<object>> AssignTaskAsync(TaskAssignDTO dto);
        System.Threading.Tasks.Task<ServiceResult<object>> DeleteTaskAsync(int id);
        System.Threading.Tasks.Task<ServiceResult<object>> SearchTasksAsync(TaskFilterDTO filter);
    }

    public class TaskService : ITaskService
    {
        private readonly TaskDbContext _context;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly IDistributedCache _cache;
        private readonly ILogger<TaskService> _logger;
        private readonly ILocalizer _localizer;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly AuthGrpc.AuthGrpcClient _authGrpc;

        private const string AllTasksCacheKey = "AllTasks";
        private const string MyTasksPrefix = "MyTasks_User_";
        private const string TaskByIdPrefix = "TaskById_";
        private readonly DistributedCacheEntryOptions _cacheOptions;

        public TaskService(
            TaskDbContext context, IHubContext<TaskHub> hubContext, IDistributedCache cache,
            IConfiguration configuration, ILogger<TaskService> logger, ILocalizer localizer,
            IPublishEndpoint publishEndpoint, AuthGrpc.AuthGrpcClient authGrpc)
        {
            _context = context;
            _hubContext = hubContext;
            _cache = cache;
            _logger = logger;
            _localizer = localizer;
            _publishEndpoint = publishEndpoint;
            _authGrpc = authGrpc;

            var slidingMin = configuration.GetValue("Cache:SlidingExpirationMinutes", 5);
            var absoluteMin = configuration.GetValue("Cache:AbsoluteExpirationMinutes", 30);
            _cacheOptions = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(slidingMin),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(absoluteMin)
            };
        }

        private async System.Threading.Tasks.Task InvalidateCacheAsync(params string[] keys)
        {
            foreach (var key in keys) await _cache.RemoveAsync(key);
        }

        public async System.Threading.Tasks.Task<ServiceResult<List<object>>> GetAllTasksAsync()
        {
            var cached = await _cache.GetStringAsync(AllTasksCacheKey);
            if (cached != null)
            {
                var data = JsonSerializer.Deserialize<List<object>>(cached);
                return ServiceResult<List<object>>.Ok(data ?? new(), "Cache");
            }

            var tasks = await _context.TaskItems
                .Select(t => new { t.TaskId, t.TaskName, t.Description, t.AssignedTo, t.AssignedToUserName, Status = t.Status.ToString(), t.CreatedAt, t.UpdatedAt })
                .ToListAsync<object>();

            await _cache.SetStringAsync(AllTasksCacheKey, JsonSerializer.Serialize(tasks), _cacheOptions);
            return ServiceResult<List<object>>.Ok(tasks, "Database");
        }

        public async System.Threading.Tasks.Task<ServiceResult<List<object>>> GetMyTasksAsync(int userId)
        {
            string cacheKey = $"{MyTasksPrefix}{userId}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                var data = JsonSerializer.Deserialize<List<object>>(cached);
                return ServiceResult<List<object>>.Ok(data ?? new(), "Cache");
            }

            var tasks = await _context.TaskItems
                .Where(t => t.AssignedTo == userId)
                .Select(t => new { t.TaskId, t.TaskName, t.Description, t.AssignedTo, t.AssignedToUserName, Status = t.Status.ToString(), t.CreatedAt, t.UpdatedAt })
                .ToListAsync<object>();

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(tasks), _cacheOptions);
            return ServiceResult<List<object>>.Ok(tasks, "Database");
        }

        public async System.Threading.Tasks.Task<ServiceResult<object>> GetTaskByIdAsync(int id)
        {
            string cacheKey = $"{TaskByIdPrefix}{id}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                var data = JsonSerializer.Deserialize<object>(cached);
                return ServiceResult<object>.Ok(data!, "Cache");
            }

            var task = await _context.TaskItems
                .Where(t => t.TaskId == id)
                .Select(t => new { t.TaskId, t.TaskName, t.Description, t.AssignedTo, t.AssignedToUserName, Status = t.Status.ToString(), t.CreatedAt, t.UpdatedAt })
                .FirstOrDefaultAsync();

            if (task == null) return ServiceResult<object>.NotFound(_localizer.Get("task.not_found"));

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(task), _cacheOptions);
            return ServiceResult<object>.Ok(task, "Database");
        }

        public async System.Threading.Tasks.Task<ServiceResult<object>> CreateTaskAsync(TaskCreateDTO dto)
        {
            string? assignedUserName = null;
            if (dto.AssignedTo.HasValue)
            {
                try
                {
                    var userInfo = await _authGrpc.GetUserInfoAsync(new GetUserInfoRequest { UserId = dto.AssignedTo.Value });
                    if (userInfo.Found) assignedUserName = userInfo.FullName;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to get user info from Auth service"); }
            }

            var task = new TaskItem
            {
                TaskName = dto.TaskName,
                Description = dto.Description,
                AssignedTo = dto.AssignedTo,
                AssignedToUserName = assignedUserName,
                Status = TaskItemStatus.Todo,
                CreatedAt = DateTime.Now
            };

            await _context.TaskItems.AddAsync(task);
            await _context.SaveChangesAsync();

            var keys = new List<string> { AllTasksCacheKey };
            if (task.AssignedTo.HasValue) keys.Add($"{MyTasksPrefix}{task.AssignedTo.Value}");
            await InvalidateCacheAsync(keys.ToArray());

            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate", task.TaskId, task.TaskName, task.Description, task.AssignedTo, task.Status.ToString(), assignedUserName);

            await _publishEndpoint.Publish(new TaskCreatedEvent
            {
                TaskId = task.TaskId, TaskName = task.TaskName, Description = task.Description,
                AssignedToUserId = task.AssignedTo, AssignedToUserName = assignedUserName,
                Status = task.Status.ToString(), CreatedAt = task.CreatedAt
            });

            return ServiceResult<object>.Created(new { task.TaskId }, _localizer.Get("task.created"));
        }

        public async System.Threading.Tasks.Task<ServiceResult<object>> UpdateTaskAsync(int id, TaskUpdateDTO dto)
        {
            if (id != dto.TaskId) return ServiceResult<object>.BadRequest(_localizer.Get("task.id_mismatch"));

            var task = await _context.TaskItems.FindAsync(id);
            if (task == null) return ServiceResult<object>.NotFound(_localizer.Get("task.not_found"));

            int? oldAssignedTo = task.AssignedTo;
            task.TaskName = dto.TaskName ?? task.TaskName;
            task.Description = dto.Description ?? task.Description;

            if (Enum.TryParse<TaskItemStatus>(dto.Status, true, out var newStatus))
                task.Status = newStatus;
            else
                return ServiceResult<object>.BadRequest(_localizer.Get("task.invalid_status"));

            if (dto.AssignedTo.HasValue && dto.AssignedTo != task.AssignedTo)
            {
                task.AssignedTo = dto.AssignedTo;
                try
                {
                    var userInfo = await _authGrpc.GetUserInfoAsync(new GetUserInfoRequest { UserId = dto.AssignedTo.Value });
                    if (userInfo.Found) task.AssignedToUserName = userInfo.FullName;
                }
                catch { }
            }

            task.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var keys = new List<string> { AllTasksCacheKey, $"{TaskByIdPrefix}{id}" };
            if (oldAssignedTo.HasValue) keys.Add($"{MyTasksPrefix}{oldAssignedTo.Value}");
            if (task.AssignedTo.HasValue) keys.Add($"{MyTasksPrefix}{task.AssignedTo.Value}");
            await InvalidateCacheAsync(keys.ToArray());

            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate", task.TaskId, task.TaskName, task.Description, task.AssignedTo, task.Status.ToString());

            await _publishEndpoint.Publish(new TaskUpdatedEvent { TaskId = task.TaskId, TaskName = task.TaskName, Status = task.Status.ToString(), AssignedToUserId = task.AssignedTo, UpdatedAt = task.UpdatedAt ?? DateTime.Now });

            return ServiceResult<object>.Ok(null, _localizer.Get("task.updated"));
        }

        public async System.Threading.Tasks.Task<ServiceResult<object>> AssignTaskAsync(TaskAssignDTO dto)
        {
            var task = await _context.TaskItems.FindAsync(dto.TaskId);
            if (task == null) return ServiceResult<object>.NotFound(_localizer.Get("task.not_found"));

            string? newUserName = null;
            try
            {
                var userInfo = await _authGrpc.GetUserInfoAsync(new GetUserInfoRequest { UserId = dto.NewAssignedToUserId });
                if (!userInfo.Found) return ServiceResult<object>.BadRequest(_localizer.Get("task.user_not_found"));
                newUserName = userInfo.FullName;
            }
            catch { return ServiceResult<object>.BadRequest("Auth service unavailable"); }

            int? oldAssignedTo = task.AssignedTo;
            task.AssignedTo = dto.NewAssignedToUserId;
            task.AssignedToUserName = newUserName;
            task.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var keys = new List<string> { AllTasksCacheKey, $"{TaskByIdPrefix}{dto.TaskId}", $"{MyTasksPrefix}{dto.NewAssignedToUserId}" };
            if (oldAssignedTo.HasValue) keys.Add($"{MyTasksPrefix}{oldAssignedTo.Value}");
            await InvalidateCacheAsync(keys.ToArray());

            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate", task.TaskId, task.TaskName, task.Description, task.AssignedTo, task.Status.ToString());

            await _publishEndpoint.Publish(new TaskAssignedEvent
            {
                TaskId = dto.TaskId, TaskName = task.TaskName, OldAssignedToUserId = oldAssignedTo,
                NewAssignedToUserId = dto.NewAssignedToUserId, NewAssignedToUserName = newUserName ?? "",
                AssignedAt = DateTime.Now
            });

            return ServiceResult<object>.Ok(null, _localizer.Get("task.assigned", newUserName ?? ""));
        }

        public async System.Threading.Tasks.Task<ServiceResult<object>> DeleteTaskAsync(int id)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.TaskId == id);
            if (task == null) return ServiceResult<object>.NotFound(_localizer.Get("task.not_found"));

            int? assignedTo = task.AssignedTo;
            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();

            var keys = new List<string> { AllTasksCacheKey, $"{TaskByIdPrefix}{id}" };
            if (assignedTo.HasValue) keys.Add($"{MyTasksPrefix}{assignedTo.Value}");
            await InvalidateCacheAsync(keys.ToArray());

            await _hubContext.Clients.All.SendAsync("ReceiveTaskDelete", id);
            await _publishEndpoint.Publish(new TaskDeletedEvent { TaskId = id, TaskName = task.TaskName, AssignedToUserId = assignedTo, DeletedAt = DateTime.Now });

            return ServiceResult<object>.Ok(null, _localizer.Get("task.deleted"));
        }

        public async System.Threading.Tasks.Task<ServiceResult<object>> SearchTasksAsync(TaskFilterDTO filter)
        {
            var query = _context.TaskItems.AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var kw = filter.Keyword.ToLower();
                query = query.Where(t => t.TaskName.ToLower().Contains(kw) || (t.Description != null && t.Description.ToLower().Contains(kw)));
            }
            if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<TaskItemStatus>(filter.Status, true, out var status))
                query = query.Where(t => t.Status == status);
            if (filter.AssignedTo.HasValue)
                query = query.Where(t => t.AssignedTo == filter.AssignedTo.Value);
            if (filter.FromDate.HasValue)
                query = query.Where(t => t.CreatedAt >= filter.FromDate.Value);
            if (filter.ToDate.HasValue)
                query = query.Where(t => t.CreatedAt <= filter.ToDate.Value);

            var totalCount = await query.CountAsync();
            var tasks = await query.OrderByDescending(t => t.CreatedAt).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
                .Select(t => new { t.TaskId, t.TaskName, t.Description, t.Status, t.AssignedTo, t.AssignedToUserName, t.CreatedAt, t.UpdatedAt })
                .ToListAsync<object>();

            var pagedResult = PagedResult<object>.Create(tasks, totalCount, filter.Page, filter.PageSize);
            return ServiceResult<object>.Ok(pagedResult);
        }
    }

    // DTOs
    public class TaskCreateDTO
    {
        public string TaskName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? AssignedTo { get; set; }
    }

    public class TaskUpdateDTO
    {
        public int TaskId { get; set; }
        public string? TaskName { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? AssignedTo { get; set; }
    }

    public class TaskAssignDTO
    {
        public int TaskId { get; set; }
        public int NewAssignedToUserId { get; set; }
    }

    public class TaskFilterDTO
    {
        public string? Keyword { get; set; }
        public string? Status { get; set; }
        public int? AssignedTo { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
