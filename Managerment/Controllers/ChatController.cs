using API.Utils;
using Managerment.DTO;
using Managerment.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Managerment.Controllers
{
    [ApiController]
    [Route("api/v1/chat")]
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("groups")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDTO request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _chatService.CreateGroupAsync(userId, request);
            return StatusCode(result.StatusCode, new { Message = result.Message, Data = result.Data });
        }

        [HttpGet("groups")]
        public async Task<IActionResult> GetMyGroups()
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _chatService.GetMyGroupsAsync(userId);
            return Ok(new { Data = result.Data });
        }

        [HttpGet("groups/{groupId}/messages")]
        public async Task<IActionResult> GetMessages(int groupId, [FromQuery] int? cursor = null, [FromQuery] int pageSize = 50)
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _chatService.GetMessagesAsync(userId, groupId, cursor, pageSize);

            // Parse NextCursor from message
            int? nextCursor = null;
            if (result.Message != null && result.Message.StartsWith("NextCursor:"))
            {
                int.TryParse(result.Message.Replace("NextCursor:", ""), out var parsed);
                nextCursor = parsed;
            }

            return StatusCode(result.StatusCode, new
            {
                Data = result.Data,
                NextCursor = nextCursor,
                HasMore = nextCursor.HasValue
            });
        }

        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageDTO request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _chatService.SendMessageAsync(userId, request);
            return StatusCode(result.StatusCode, new { Message = result.Message, Data = result.Data });
        }

        [HttpDelete("messages/{messageId}")]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _chatService.DeleteMessageAsync(userId, messageId);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpPost("messages/{messageId}/reactions")]
        public async Task<IActionResult> ReactToMessage(int messageId, [FromBody] ReactMessageDTO request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            request.MessageId = messageId;
            var result = await _chatService.ReactToMessageAsync(userId, request);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpDelete("messages/{messageId}/reactions")]
        public async Task<IActionResult> RemoveReaction(int messageId)
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _chatService.RemoveReactionAsync(userId, messageId);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpPut("groups/{groupId}/read")]
        public async Task<IActionResult> MarkAsRead(int groupId)
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _chatService.MarkAsReadAsync(userId, groupId);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }
    }
}
