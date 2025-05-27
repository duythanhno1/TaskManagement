using System.Text.Json;
using API.Utils;
using Managerment.ApplicationContext;
using Managerment.DTO;
using Managerment.Model;
using Managerment.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Managerment.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO request) 
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var checkEmail = await _context.Users.FirstOrDefaultAsync(x => x.Email.ToLower().Equals(request.Email.ToLower()));
            if (checkEmail != null)
            {
                return Conflict(new { Message = "Email Already Exist" });
            }

            var createNewAccount = new User
            {
                Email = request.Email, 
                FullName = request.FullName, 
                PasswordHash = Ultil.GenerateMD5(request.Password),
                PhoneNumber = request.PhoneNumber, 
                Role = request.Role,
                CreatedAt = DateTime.Now
            };

            await _context.Users.AddAsync(createNewAccount);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Register Success" });
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

                var checkLogin = await _context.Users.FirstOrDefaultAsync(x => x.Email.ToLower().Equals(request.Email.ToLower())
                                                                          && x.PasswordHash.Equals(Ultil.GenerateMD5(request.Password)));
                if (checkLogin == null)
                {
                    return NotFound(new { Message = "Email or Password Incorrect" });
                }
                var token = JWTHandler.GenerateJWT(checkLogin, _configuration["JWT:SecretKey"]!);
                return Ok(new { Message = "Login Success", Token = token });
            }
            catch (Exception ex) { 
            
                return StatusCode(500, ex.ToString());
            }
        }
    }
}
