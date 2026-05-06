using Microsoft.EntityFrameworkCore;
using QueueManagementSystem.Models;

namespace QueueManagementSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Token> Tokens { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // FIX: Ensure Username is unique at the DB level to prevent
            // race conditions when two simultaneous registrations slip past
            // the application-level duplicate check.
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
        }
    }
}
