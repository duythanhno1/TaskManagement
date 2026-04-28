using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Shared.Infrastructure.Auth
{
    public class JwtMiddleware : IMiddleware
    {
        private readonly IConfiguration _configuration;
        private readonly string[] _anonymousPaths;

        public JwtMiddleware(IConfiguration configuration)
        {
            _configuration = configuration;
            _anonymousPaths = configuration.GetSection("Auth:AnonymousPaths").Get<string[]>() ?? Array.Empty<string>();
        }

        public async System.Threading.Tasks.Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            // Allow anonymous access for specified paths
            if (_anonymousPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase))
                || context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/hangfire", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue("Authorization", out var authorizationHeader)
                || !authorizationHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await RespondWithUnauthorized(context);
                return;
            }

            try
            {
                string token = authorizationHeader.ToString()["Bearer ".Length..].Trim();
                ClaimsPrincipal claimsPrincipal = ValidateToken(token);
                context.User = claimsPrincipal;
            }
            catch (Exception)
            {
                await RespondWithUnauthorized(context);
                return;
            }

            await next(context);
        }

        private static async System.Threading.Tasks.Task RespondWithUnauthorized(HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Unauthorized" }));
        }

        private ClaimsPrincipal ValidateToken(string jwtToken)
        {
            string secretKey = _configuration["JWT:SecretKey"]!;
            SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(secretKey));

            TokenValidationParameters tokenValidationParameters = new()
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKey = securityKey
            };

            JwtSecurityTokenHandler tokenHandler = new();
            return tokenHandler.ValidateToken(jwtToken, tokenValidationParameters, out _);
        }
    }
}
