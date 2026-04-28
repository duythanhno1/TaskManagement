using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Interfaces;
using Task.API.Models;

namespace Task.API.Data
{
    public class TaskDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public TaskDbContext(DbContextOptions<TaskDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.Now;
                }
            }

            var result = await base.SaveChangesAsync(cancellationToken);
            return result;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TaskItem>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.EntityName, a.EntityId });
            modelBuilder.Entity<AuditLog>().HasIndex(a => a.Timestamp);
        }
    }
}
