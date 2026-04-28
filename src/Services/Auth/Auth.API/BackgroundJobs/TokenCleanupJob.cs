using Auth.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.BackgroundJobs
{
    public class TokenCleanupJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TokenCleanupJob> _logger;

        public TokenCleanupJob(IServiceScopeFactory scopeFactory, ILogger<TokenCleanupJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task Execute()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

            var expiredTokens = await context.RefreshTokens
                .Where(t => t.ExpiresAt < DateTime.UtcNow || t.IsRevoked)
                .ToListAsync();

            if (expiredTokens.Count > 0)
            {
                context.RefreshTokens.RemoveRange(expiredTokens);
                await context.SaveChangesAsync();
                _logger.LogInformation("Cleaned up {Count} expired/revoked refresh tokens", expiredTokens.Count);
            }
        }
    }
}
