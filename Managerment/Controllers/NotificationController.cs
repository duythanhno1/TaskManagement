using API.Utils;
using Managerment.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Managerment.Controllers
{
    [ApiController]
    [Route("api/v1/notifications")]
    [Authorize]
    [EnableRateLimiting("general")]
    public class NotificationController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _notificationService.GetMyNotificationsAsync(userId, page, pageSize);

            // Parse UnreadCount from message
            int.TryParse(result.Message?.Replace("UnreadCount:", ""), out var unreadCount);

            return Ok(new { Data = result.Data, UnreadCount = unreadCount });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _notificationService.MarkAsReadAsync(userId, id);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = JWTHandler.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _notificationService.MarkAllAsReadAsync(userId);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }
    }
}
