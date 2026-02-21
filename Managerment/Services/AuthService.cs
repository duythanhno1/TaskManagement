using API.Utils;
using Managerment.ApplicationContext;
using Managerment.DTO;
using Managerment.Interfaces;
using Managerment.Model;
using Managerment.Util;
using Microsoft.EntityFrameworkCore;

namespace Managerment.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<ServiceResult<object>> RegisterAsync(RegisterDTO dto)
        {
            var checkEmail = await _context.Users
                .FirstOrDefaultAsync(x => x.Email.ToLower().Equals(dto.Email.ToLower()));

            if (checkEmail != null)
            {
                return ServiceResult<object>.Conflict("Email Already Exist");
            }

            var newUser = new User
            {
                Email = dto.Email,
                FullName = dto.FullName,
                PasswordHash = Ultil.GenerateMD5(dto.Password),
                PhoneNumber = dto.PhoneNumber,
                Role = dto.Role,
                CreatedAt = DateTime.Now
            };

            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();

            return ServiceResult<object>.Ok(null, "Register Success");
        }

        public async Task<ServiceResult<object>> LoginAsync(LoginDTO dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email.ToLower().Equals(dto.Email.ToLower())
                    && x.PasswordHash.Equals(Ultil.GenerateMD5(dto.Password)));

            if (user == null)
            {
                return ServiceResult<object>.NotFound("Email or Password Incorrect");
            }

            var token = JWTHandler.GenerateJWT(user, _configuration["JWT:SecretKey"]!);
            return ServiceResult<object>.Ok(new { Token = token }, "Login Success");
        }
    }
}
