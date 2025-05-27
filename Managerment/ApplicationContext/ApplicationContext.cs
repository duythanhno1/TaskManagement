using Managerment.Model;
using Microsoft.EntityFrameworkCore;

namespace Managerment.ApplicationContext
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }

        public DbSet<TaskItem> TaskItems { get; set; } 

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configure relationship between TaskItem and User
            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.AssignedToUser)
                .WithMany() 
                .HasForeignKey(t => t.AssignedTo)
                .IsRequired(false) 
                .OnDelete(DeleteBehavior.SetNull); 
        }
    }
}
