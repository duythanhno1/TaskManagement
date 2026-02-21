using API.Utils;
using Managerment.DTO;
using Managerment.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Managerment.Controllers
{
    [ApiController]
    [Route("api/v1/tasks")]
    [Authorize]
    [EnableRateLimiting("general")]
    public class TaskController : Controller
    {
        private readonly ITaskService _taskService;

        public TaskController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTasks()
        {
            var result = await _taskService.GetAllTasksAsync();
            return Ok(new { Data = result.Data, Source = result.Message });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var result = await _taskService.GetAllUsersAsync();
            return Ok(new { Data = result.Data });
        }

        [HttpGet("my-tasks")]
        public async Task<IActionResult> GetMyTasks()
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0)
            {
                return Unauthorized();
            }

            var result = await _taskService.GetMyTasksAsync(userId);
            return Ok(new { Data = result.Data, Source = result.Message });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] TaskCreateDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _taskService.CreateTaskAsync(request);
            return CreatedAtAction(nameof(GetTaskById), new { id = ((dynamic)result.Data).TaskId },
                new { Message = result.Message, Data = result.Data });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTaskById(int id)
        {
            var result = await _taskService.GetTaskByIdAsync(id);
            if (!result.Success)
            {
                return NotFound(new { Message = result.Message });
            }
            return Ok(new { Data = result.Data, Source = result.Message });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskUpdateDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _taskService.UpdateTaskAsync(id, request);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpPut("assign")]
        public async Task<IActionResult> AssignTask([FromBody] TaskAssignDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _taskService.AssignTaskAsync(request);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var result = await _taskService.DeleteTaskAsync(id);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }
    }
}