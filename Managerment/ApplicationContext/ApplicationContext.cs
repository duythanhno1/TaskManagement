using Managerment.Model;
using Microsoft.EntityFrameworkCore;

namespace Managerment.ApplicationContext
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<ChatGroup> ChatGroups { get; set; }
        public DbSet<ChatGroupMember> ChatGroupMembers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<MessageReaction> MessageReactions { get; set; }
        public DbSet<MessageReadStatus> MessageReadStatuses { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
        }
    }
}
