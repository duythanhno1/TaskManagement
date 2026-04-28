using Microsoft.EntityFrameworkCore;
using Notification.API.Models;

namespace Notification.API.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }
        public DbSet<NotificationItem> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<NotificationItem>().HasIndex(n => new { n.UserId, n.IsRead });
        }
    }
}
