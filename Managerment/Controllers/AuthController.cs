using Managerment.DTO;
using Managerment.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Managerment.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
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
            try
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

                return Ok(new { Message = result.Message, Token = ((dynamic)result.Data).Token });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.ToString());
            }
        }
    }
}
