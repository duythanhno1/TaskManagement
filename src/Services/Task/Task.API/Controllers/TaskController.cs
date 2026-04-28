using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Infrastructure.Auth;
using Task.API.Services;

namespace Task.API.Controllers
{
    [ApiController]
    [Route("api/v1/tasks")]
    [Authorize]
    public class TaskController : Controller
    {
        private readonly ITaskService _taskService;
        public TaskController(ITaskService taskService) { _taskService = taskService; }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> GetAllTasks()
        {
            var result = await _taskService.GetAllTasksAsync();
            return Ok(new { Data = result.Data, Source = result.Message });
        }

        [HttpGet("my-tasks")]
        public async System.Threading.Tasks.Task<IActionResult> GetMyTasks()
        {
            var userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();
            var result = await _taskService.GetMyTasksAsync(userId);
            return Ok(new { Data = result.Data, Source = result.Message });
        }

        [HttpPost]
        public async System.Threading.Tasks.Task<IActionResult> CreateTask([FromBody] TaskCreateDTO request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _taskService.CreateTaskAsync(request);
            return StatusCode(result.StatusCode, new { Message = result.Message, Data = result.Data });
        }

        [HttpGet("{id}")]
        public async System.Threading.Tasks.Task<IActionResult> GetTaskById(int id)
        {
            var result = await _taskService.GetTaskByIdAsync(id);
            if (!result.Success) return NotFound(new { Message = result.Message });
            return Ok(new { Data = result.Data, Source = result.Message });
        }

        [HttpPut("{id}")]
        public async System.Threading.Tasks.Task<IActionResult> UpdateTask(int id, [FromBody] TaskUpdateDTO request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _taskService.UpdateTaskAsync(id, request);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpPut("assign")]
        public async System.Threading.Tasks.Task<IActionResult> AssignTask([FromBody] TaskAssignDTO request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _taskService.AssignTaskAsync(request);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpDelete("{id}")]
        public async System.Threading.Tasks.Task<IActionResult> DeleteTask(int id)
        {
            var result = await _taskService.DeleteTaskAsync(id);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpGet("search")]
        public async System.Threading.Tasks.Task<IActionResult> SearchTasks([FromQuery] TaskFilterDTO filter)
        {
            var result = await _taskService.SearchTasksAsync(filter);
            return Ok(new { Data = result.Data });
        }
    }
}
