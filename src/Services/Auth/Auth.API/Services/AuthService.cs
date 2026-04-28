using Auth.API.Data;
using Auth.API.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Events;
using Shared.Contracts.Interfaces;
using Shared.Infrastructure;
using Shared.Infrastructure.Auth;

namespace Auth.API.Services
{
    public interface IAuthService
    {
        Task<ServiceResult<object>> RegisterAsync(RegisterDTO dto);
        Task<ServiceResult<object>> LoginAsync(LoginDTO dto);
        Task<ServiceResult<object>> RefreshTokenAsync(string refreshToken);
        Task<ServiceResult<object>> RevokeTokenAsync(int userId);
        Task<ServiceResult<List<object>>> GetAllUsersAsync();
        Task<ServiceResult<object>> GetUserByIdAsync(int userId);
        Task<ServiceResult<List<object>>> GetUsersByIdsAsync(List<int> userIds);
    }

    public class AuthService : IAuthService
    {
        private readonly AuthDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILocalizer _localizer;
        private readonly IPublishEndpoint _publishEndpoint;

        public AuthService(AuthDbContext context, IConfiguration configuration, ILocalizer localizer, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _configuration = configuration;
            _localizer = localizer;
            _publishEndpoint = publishEndpoint;
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
                PasswordHash = JwtHelper.HashPassword(dto.Password),
                PhoneNumber = dto.PhoneNumber,
                Role = dto.Role,
                CreatedAt = DateTime.Now
            };

            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();

            // Publish event for other services
            await _publishEndpoint.Publish(new UserRegisteredEvent
            {
                UserId = newUser.UserId,
                FullName = newUser.FullName,
                Email = newUser.Email,
                Role = newUser.Role,
                CreatedAt = newUser.CreatedAt
            });

            return ServiceResult<object>.Ok(null, _localizer.Get("auth.register_success"));
        }

        public async Task<ServiceResult<object>> LoginAsync(LoginDTO dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.Email.ToLower().Equals(dto.Email.ToLower()));

            if (user == null || !JwtHelper.VerifyPassword(dto.Password, user.PasswordHash))
            {
                return ServiceResult<object>.NotFound(_localizer.Get("auth.login_failed"));
            }

            var accessToken = JwtHelper.GenerateJwt(user.UserId, user.Role, _configuration["JWT:SecretKey"]!);
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

            var newAccessToken = JwtHelper.GenerateJwt(token.User.UserId, token.User.Role, _configuration["JWT:SecretKey"]!);
            var newRefreshToken = await GenerateRefreshTokenAsync(token.UserId);

            await _context.SaveChangesAsync();

            return ServiceResult<object>.Ok(new
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token,
                ExpiresIn = 7200
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

        public async Task<ServiceResult<List<object>>> GetAllUsersAsync()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email
                })
                .ToListAsync<object>();

            return ServiceResult<List<object>>.Ok(users);
        }

        public async Task<ServiceResult<object>> GetUserByIdAsync(int userId)
        {
            var user = await _context.Users
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return ServiceResult<object>.NotFound("User not found.");
            }

            return ServiceResult<object>.Ok(user);
        }

        public async Task<ServiceResult<List<object>>> GetUsersByIdsAsync(List<int> userIds)
        {
            var users = await _context.Users
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email
                })
                .ToListAsync<object>();

            return ServiceResult<List<object>>.Ok(users);
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

    // DTOs
    public class RegisterDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = "User";
    }

    public class LoginDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
