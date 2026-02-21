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
        private readonly ILocalizer _localizer;

        public AuthService(ApplicationDbContext context, IConfiguration configuration, ILocalizer localizer)
        {
            _context = context;
            _configuration = configuration;
            _localizer = localizer;
        }

        public async Task<ServiceResult<object>> RegisterAsync(RegisterDTO dto)
        {
            var checkEmail = await _context.Users
                .FirstOrDefaultAsync(x => x.Email.ToLower().Equals(dto.Email.ToLower()));

            if (checkEmail != null)
            {
                return ServiceResult<object>.Conflict(_localizer.Get("auth.email_exists"));
            }

            var newUser = new User
            {
                Email = dto.Email,
                FullName = dto.FullName,
                PasswordHash = Ultil.HashPassword(dto.Password),
                PhoneNumber = dto.PhoneNumber,
                Role = dto.Role,
                CreatedAt = DateTime.Now
            };

            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();

            return ServiceResult<object>.Ok(null, _localizer.Get("auth.register_success"));
        }

        public async Task<ServiceResult<object>> LoginAsync(LoginDTO dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email.ToLower().Equals(dto.Email.ToLower()));

            if (user == null || !Ultil.VerifyPassword(dto.Password, user.PasswordHash))
            {
                return ServiceResult<object>.NotFound(_localizer.Get("auth.login_failed"));
            }

            var token = JWTHandler.GenerateJWT(user, _configuration["JWT:SecretKey"]!);
            return ServiceResult<object>.Ok(new { Token = token }, _localizer.Get("auth.login_success"));
        }
    }
}
