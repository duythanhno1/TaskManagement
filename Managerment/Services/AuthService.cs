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

            var accessToken = JWTHandler.GenerateJWT(user, _configuration["JWT:SecretKey"]!);
            var refreshToken = await GenerateRefreshTokenAsync(user.UserId);

            return ServiceResult<object>.Ok(new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresIn = 7200 // 120 minutes in seconds
            }, _localizer.Get("auth.login_success"));
        }

        public async Task<ServiceResult<object>> RefreshTokenAsync(string refreshToken)
        {
            var token = await _context.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == refreshToken && !t.IsRevoked);

            if (token == null || token.ExpiresAt < DateTime.UtcNow)
            {
                return ServiceResult<object>.Unauthorized("Invalid or expired refresh token.");
            }

            // Rotate: revoke old, create new
            token.IsRevoked = true;

            var newAccessToken = JWTHandler.GenerateJWT(token.User, _configuration["JWT:SecretKey"]!);
            var newRefreshToken = await GenerateRefreshTokenAsync(token.UserId);

            await _context.SaveChangesAsync();

            return ServiceResult<object>.Ok(new
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token,
                ExpiresIn = 900
            });
        }

        public async Task<ServiceResult<object>> RevokeTokenAsync(int userId)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
            }

            await _context.SaveChangesAsync();

            return ServiceResult<object>.Ok(null, "All tokens revoked.");
        }

        private async Task<RefreshToken> GenerateRefreshTokenAsync(int userId)
        {
            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.Now
            };

            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }
    }
}
