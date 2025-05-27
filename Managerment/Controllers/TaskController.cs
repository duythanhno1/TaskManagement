using API.Utils;
using Managerment.ApplicationContext;
using Managerment.DTO;
using Managerment.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Managerment.Model;
using Microsoft.Extensions.Caching.Memory; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Managerment.Controllers
{
    [ApiController]
    [Route("api/v1/tasks")]
    [Authorize]
    public class TaskController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly IMemoryCache _cache; // Thêm IMemoryCache

        // Cache Keys
        private const string AllTasksCacheKey = "AllTasks";
        private const string MyTasksPrefixCacheKey = "MyTasks_User_"; // Sẽ nối thêm UserId
        private const string TaskByIdPrefixCacheKey = "TaskById_"; // Sẽ nối thêm TaskId

        private readonly MemoryCacheEntryOptions _cacheOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(5)) // Hết hạn nếu không truy cập trong 5 phút
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30)); // Hết hạn tuyệt đối sau 30 phút

        public TaskController(ApplicationDbContext context, IHubContext<TaskHub> hubContext, IMemoryCache cache)
        {
            _context = context;
            _hubContext = hubContext;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTasks()
        {
            if (_cache.TryGetValue(AllTasksCacheKey, out List<object> cachedTasks))
            {
                return Ok(new { Data = cachedTasks, Source = "Cache" });
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
            return Ok(new { Data = tasks, Source = "Database" });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email
                })
                .ToListAsync();
            return Ok(new { Data = users });
        }

        [HttpGet("my-tasks")]
        public async Task<IActionResult> GetMyTasks()
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0)
            {
                return Unauthorized();
            }

            string cacheKey = $"{MyTasksPrefixCacheKey}{userId}";

            if (_cache.TryGetValue(cacheKey, out List<object> cachedTasks))
            {
                return Ok(new { Data = cachedTasks, Source = "Cache" });
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
            return Ok(new { Data = tasks, Source = "Database" });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] TaskCreateDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var task = new TaskItem
            {
                TaskName = request.TaskName,
                Description = request.Description,
                AssignedTo = request.AssignedTo,
                Status = Managerment.Model.TaskStatus.Todo,
                CreatedAt = DateTime.Now
            };

            // Invalidate cache trước khi thêm task mới
            _cache.Remove(AllTasksCacheKey);
            if (task.AssignedTo.HasValue)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{task.AssignedTo.Value}");
            }

            await _context.TaskItems.AddAsync(task);
            await _context.SaveChangesAsync();

            var assignedUser = request.AssignedTo.HasValue ? await _context.Users.FindAsync(request.AssignedTo.Value) : null;

            // Gửi SignalR notification sau khi lưu thành công
            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate",
                task.TaskId,
                task.TaskName,
                task.Description,
                task.AssignedTo,
                task.Status.ToString(),
                assignedUser?.FullName); // Thêm tên người được gán

            if (assignedUser != null)
            {
                await _hubContext.Clients.User(assignedUser.UserId.ToString())
                    .SendAsync("ReceiveTaskAssignmentNotification", $"You have been assigned a new task: {task.TaskName}");
            }

            return CreatedAtAction(nameof(GetTaskById), new { id = task.TaskId }, 
                new { Message = "Task created successfully", Data = new { task.TaskId } });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTaskById(int id)
        {
            string cacheKey = $"{TaskByIdPrefixCacheKey}{id}";
            if (_cache.TryGetValue(cacheKey, out object cachedTask))
            {
                return Ok(new { Data = cachedTask, Source = "Cache" });
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
                return NotFound(new { Message = "Task not found." });
            }
            _cache.Set(cacheKey, task, _cacheOptions);
            return Ok(new { Data = task, Source = "Database" });
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskUpdateDTO request)
        {
            if (!ModelState.IsValid || id != request.TaskId)
            {
                return BadRequest(ModelState);
            }

            var task = await _context.TaskItems.FindAsync(id);
            if (task == null)
            {
                return NotFound(new { Message = "Task not found." });
            }

            // Lưu lại thông tin người được gán cũ (nếu có) để invalidate cache
            int? oldAssignedTo = task.AssignedTo;

            task.TaskName = request.TaskName ?? task.TaskName;
            task.Description = request.Description ?? task.Description;

            if (Enum.TryParse<Managerment.Model.TaskStatus>(request.Status, true, out var newStatus))
            {
                task.Status = newStatus;
            }
            else
            {
                return BadRequest(new { Message = "Invalid status value." });
            }

            if (request.AssignedTo.HasValue && request.AssignedTo != task.AssignedTo)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{task.AssignedTo}"); // Xóa cache của user cũ nếu task được gán cho người khác
                task.AssignedTo = request.AssignedTo;
            }


            task.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Cache Invalidation
            _cache.Remove(AllTasksCacheKey);
            _cache.Remove($"{TaskByIdPrefixCacheKey}{id}");
            if (oldAssignedTo.HasValue)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{oldAssignedTo.Value}");
            }
            if (task.AssignedTo.HasValue) // Sau khi cập nhật
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{task.AssignedTo.Value}");
            }


            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate",
                task.TaskId,
                task.TaskName,
                task.Description,
                task.AssignedTo,
                task.Status.ToString());

            return Ok(new { Message = "Task updated successfully." });
        }

        [HttpPut("assign")]
        public async Task<IActionResult> AssignTask([FromBody] TaskAssignDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var task = await _context.TaskItems.FindAsync(request.TaskId);
            if (task == null)
            {
                return NotFound(new { Message = "Task not found." });
            }

            var newAssignee = await _context.Users.FindAsync(request.NewAssignedToUserId);
            if (newAssignee == null)
            {
                return BadRequest(new { Message = "New assigned user not found." });
            }

            int? oldAssignedTo = task.AssignedTo;
            task.AssignedTo = request.NewAssignedToUserId;
            task.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Cache Invalidation
            _cache.Remove(AllTasksCacheKey); // Vì thông tin assigned user trong list all tasks thay đổi
            _cache.Remove($"{TaskByIdPrefixCacheKey}{request.TaskId}"); // Vì thông tin task này thay đổi
            if (oldAssignedTo.HasValue)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{oldAssignedTo.Value}"); // Xóa cache "my-tasks" của user cũ
            }
            _cache.Remove($"{MyTasksPrefixCacheKey}{request.NewAssignedToUserId}"); // Xóa cache "my-tasks" của user mới


            await _hubContext.Clients.All.SendAsync("ReceiveTaskUpdate",
                task.TaskId,
                task.TaskName,
                task.Description,
                task.AssignedTo,
                task.Status.ToString());

            await _hubContext.Clients.User(newAssignee.UserId.ToString())
                             .SendAsync("ReceiveTaskAssignmentNotification", $"You have been assigned to task: {task.TaskName}");


            return Ok(new { Message = $"Task assigned to {newAssignee.FullName} successfully." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var task = await _context.TaskItems
                .Include(t => t.AssignedToUser)
                .FirstOrDefaultAsync(t => t.TaskId == id);
        
            if (task == null)
            {
                return NotFound(new { Message = "Task not found." });
            }

            int? assignedToUser = task.AssignedTo;

            // Invalidate cache trước khi xóa
            _cache.Remove(AllTasksCacheKey);
            _cache.Remove($"{TaskByIdPrefixCacheKey}{id}");
            if (assignedToUser.HasValue)
            {
                _cache.Remove($"{MyTasksPrefixCacheKey}{assignedToUser.Value}");
            }

            _context.TaskItems.Remove(task);
            await _context.SaveChangesAsync();

            // Gửi SignalR notification sau khi xóa thành công
            await _hubContext.Clients.All.SendAsync("ReceiveTaskDelete", id);

            return Ok(new { Message = "Task deleted successfully." });
        }
    }
}