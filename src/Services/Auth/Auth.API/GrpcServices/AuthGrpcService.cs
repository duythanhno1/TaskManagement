using Auth.API.Data;
using Auth.API.Protos;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Auth.API.GrpcServices
{
    public class AuthGrpcService : AuthGrpc.AuthGrpcBase
    {
        private readonly AuthDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthGrpcService> _logger;

        public AuthGrpcService(AuthDbContext context, IConfiguration configuration, ILogger<AuthGrpcService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public override async Task<ValidateTokenResponse> ValidateToken(ValidateTokenRequest request, ServerCallContext context)
        {
            try
            {
                var secretKey = _configuration["JWT:SecretKey"]!;
                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(request.Token, new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = securityKey
                }, out _);

                var userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "User";

                var user = await _context.Users.FindAsync(userId);

                return new ValidateTokenResponse
                {
                    IsValid = true,
                    UserId = userId,
                    Role = role,
                    FullName = user?.FullName ?? ""
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return new ValidateTokenResponse { IsValid = false };
            }
        }

        public override async Task<GetUserInfoResponse> GetUserInfo(GetUserInfoRequest request, ServerCallContext context)
        {
            var user = await _context.Users.FindAsync(request.UserId);

            if (user == null)
            {
                return new GetUserInfoResponse { Found = false };
            }

            return new GetUserInfoResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role,
                Found = true
            };
        }

        public override async Task<GetUsersByIdsResponse> GetUsersByIds(GetUsersByIdsRequest request, ServerCallContext context)
        {
            var userIds = request.UserIds.ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => new UserInfo
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email
                })
                .ToListAsync();

            var response = new GetUsersByIdsResponse();
            response.Users.AddRange(users);
            return response;
        }
    }
}
