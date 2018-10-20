using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WagahighChoices.Kaoruko.Models
{
    public class KaorukoDbContext : DbContext
    {
        public KaorukoDbContext(DbContextOptions<KaorukoDbContext> options)
          : base(options)
        { }

        public DbSet<SearchResult> SearchResults { get; set; }

        public DbSet<Worker> Workers { get; set; }

        public DbSet<WorkerJob> WorkerJobs { get; set; }

        public DbSet<WorkerLog> WorkerLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SearchResult>()
                .HasAlternateKey(x => x.Choices);

            modelBuilder.Entity<WorkerJob>()
                .HasAlternateKey(x => x.Choices);
        }
    }

    public class DesignTimeKaorukoDbContextFactory : IDesignTimeDbContextFactory<KaorukoDbContext>
    {
        public KaorukoDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<KaorukoDbContext>()
                .UseSqlite("Data Source=:memory:");
            return new KaorukoDbContext(builder.Options);
        }
    }
}
