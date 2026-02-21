using System.Text.Json;
using Managerment.Interfaces;
using Managerment.Model;
using Microsoft.EntityFrameworkCore;

namespace Managerment.ApplicationContext
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<ChatGroup> ChatGroups { get; set; }
        public DbSet<ChatGroupMember> ChatGroupMembers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<MessageReaction> MessageReactions { get; set; }
        public DbSet<MessageReadStatus> MessageReadStatuses { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        // Audit Trail: override SaveChangesAsync to track changes
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Soft Delete interception: convert Delete → set IsDeleted=true
            foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.Now;
                }
            }

            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);

            if (auditEntries.Count > 0)
            {
                await AuditLogs.AddRangeAsync(auditEntries, cancellationToken);
                await base.SaveChangesAsync(cancellationToken);
            }

            return result;
        }

        private List<AuditLog> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditLog>();

            int? currentUserId = null;
            var userClaim = _httpContextAccessor?.HttpContext?.User?.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userClaim != null && int.TryParse(userClaim.Value, out var uid))
            {
                currentUserId = uid;
            }

            foreach (var entry in ChangeTracker.Entries())
            {
                // Skip AuditLog itself to avoid infinite loop
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditLog = new AuditLog
                {
                    EntityName = entry.Entity.GetType().Name,
                    UserId = currentUserId,
                    Timestamp = DateTime.Now
                };

                // Get primary key value
                var primaryKey = entry.Properties
                    .FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                auditLog.EntityId = primaryKey?.CurrentValue?.ToString() ?? "N/A";

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditLog.Action = "Create";
                        auditLog.NewValues = JsonSerializer.Serialize(
                            entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                        break;

                    case EntityState.Modified:
                        auditLog.Action = "Update";
                        var changedProps = entry.Properties
                            .Where(p => p.IsModified)
                            .ToList();
                        auditLog.OldValues = JsonSerializer.Serialize(
                            changedProps.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                        auditLog.NewValues = JsonSerializer.Serialize(
                            changedProps.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                        auditLog.ChangedColumns = JsonSerializer.Serialize(
                            changedProps.Select(p => p.Metadata.Name));
                        break;

                    case EntityState.Deleted:
                        auditLog.Action = "Delete";
                        auditLog.OldValues = JsonSerializer.Serialize(
                            entry.Properties.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                        break;
                }

                auditEntries.Add(auditLog);
            }

            return auditEntries;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global Query Filters — Soft Delete
            modelBuilder.Entity<TaskItem>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<ChatMessage>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<ChatGroup>().HasQueryFilter(e => !e.IsDeleted);

            // User
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // TaskItem -> User
            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.AssignedToUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedTo)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // ChatGroup -> User (creator)
            modelBuilder.Entity<ChatGroup>()
                .HasOne(g => g.CreatedByUser)
                .WithMany()
                .HasForeignKey(g => g.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ChatGroupMember -> ChatGroup
            modelBuilder.Entity<ChatGroupMember>()
                .HasOne(m => m.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // ChatGroupMember -> User
            modelBuilder.Entity<ChatGroupMember>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: 1 user per group
            modelBuilder.Entity<ChatGroupMember>()
                .HasIndex(m => new { m.GroupId, m.UserId })
                .IsUnique();

            // ChatMessage -> ChatGroup
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Group)
                .WithMany(g => g.Messages)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            // ChatMessage -> User (sender)
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.SenderUser)
                .WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // MessageReaction -> ChatMessage
            modelBuilder.Entity<MessageReaction>()
                .HasOne(r => r.Message)
                .WithMany(m => m.Reactions)
                .HasForeignKey(r => r.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // MessageReaction -> User
            modelBuilder.Entity<MessageReaction>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: 1 reaction per user per message
            modelBuilder.Entity<MessageReaction>()
                .HasIndex(r => new { r.MessageId, r.UserId })
                .IsUnique();

            // MessageReadStatus -> ChatMessage
            modelBuilder.Entity<MessageReadStatus>()
                .HasOne(rs => rs.Message)
                .WithMany(m => m.ReadStatuses)
                .HasForeignKey(rs => rs.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // MessageReadStatus -> User
            modelBuilder.Entity<MessageReadStatus>()
                .HasOne(rs => rs.User)
                .WithMany()
                .HasForeignKey(rs => rs.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: 1 read status per user per message
            modelBuilder.Entity<MessageReadStatus>()
                .HasIndex(rs => new { rs.MessageId, rs.UserId })
                .IsUnique();

            // Notification -> User
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index for quick notification queries
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.IsRead });

            // AuditLog -> User (optional)
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // Index for audit log queries
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new { a.EntityName, a.EntityId });

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.Timestamp);
        }
    }
}
