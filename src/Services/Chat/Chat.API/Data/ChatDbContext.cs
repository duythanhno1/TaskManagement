using Chat.API.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Interfaces;

namespace Chat.API.Data
{
    public class ChatDbContext : DbContext
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

        public DbSet<ChatGroup> ChatGroups { get; set; }
        public DbSet<ChatGroupMember> ChatGroupMembers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<MessageReaction> MessageReactions { get; set; }
        public DbSet<MessageReadStatus> MessageReadStatuses { get; set; }

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
            return await base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ChatMessage>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<ChatGroup>().HasQueryFilter(e => !e.IsDeleted);

            modelBuilder.Entity<ChatGroupMember>().HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ChatGroupMember>().HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();

            modelBuilder.Entity<ChatMessage>().HasOne(m => m.Group).WithMany(g => g.Messages).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MessageReaction>().HasOne(r => r.Message).WithMany(m => m.Reactions).HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MessageReaction>().HasIndex(r => new { r.MessageId, r.UserId }).IsUnique();

            modelBuilder.Entity<MessageReadStatus>().HasOne(rs => rs.Message).WithMany(m => m.ReadStatuses).HasForeignKey(rs => rs.MessageId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<MessageReadStatus>().HasIndex(rs => new { rs.MessageId, rs.UserId }).IsUnique();
        }
    }
}
