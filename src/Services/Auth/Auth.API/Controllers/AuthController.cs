using Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Infrastructure.Auth;

namespace Auth.API.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var result = await _authService.RegisterAsync(request);
            return StatusCode(result.StatusCode, new { Message = result.Message });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { Message = "Email or Password is empty" });
            }

            var result = await _authService.LoginAsync(request);
            if (!result.Success)
            {
                return StatusCode(result.StatusCode, new { Message = result.Message });
            }

            return Ok(new
            {
                Message = result.Message,
                Data = result.Data
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.RefreshToken);
            if (!result.Success)
            {
                return StatusCode(result.StatusCode, new { Message = result.Message });
            }

            return Ok(new { Data = result.Data });
        }

        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeToken()
        {
            var userId = JwtHelper.GetUserIdFromHttpContext(HttpContext);
            if (userId == 0) return Unauthorized();

            var result = await _authService.RevokeTokenAsync(userId);
            return Ok(new { Message = result.Message });
        }

        [HttpGet("users")]
        [Authorize]
        public async Task<IActionResult> GetAllUsers()
        {
            var result = await _authService.GetAllUsersAsync();
            return Ok(new { Data = result.Data });
        }

        [HttpGet("users/{id}")]
        [Authorize]
        public async Task<IActionResult> GetUserById(int id)
        {
            var result = await _authService.GetUserByIdAsync(id);
            if (!result.Success) return NotFound(new { Message = result.Message });
            return Ok(new { Data = result.Data });
        }

        [HttpPost("users/batch")]
        [Authorize]
        public async Task<IActionResult> GetUsersByIds([FromBody] List<int> userIds)
        {
            var result = await _authService.GetUsersByIdsAsync(userIds);
            return Ok(new { Data = result.Data });
        }
    }
}
