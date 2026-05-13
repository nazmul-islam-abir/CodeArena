using Microsoft.EntityFrameworkCore;
using MyMvcApp.Models;

namespace MyMvcApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Problem> Problems { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<TestCase> TestCases { get; set; }
        public DbSet<Contest> Contests { get; set; }
        public DbSet<ContestProblem> ContestProblems { get; set; }
        public DbSet<ContestRegistration> ContestRegistrations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure composite key for ContestRegistration
            modelBuilder.Entity<ContestRegistration>()
                .HasKey(cr => new { cr.ContestId, cr.UserName });

            // Configure ContestProblem relationships
            modelBuilder.Entity<ContestProblem>()
                .HasOne(cp => cp.Contest)
                .WithMany()
                .HasForeignKey(cp => cp.ContestId);

            modelBuilder.Entity<ContestProblem>()
                .HasOne(cp => cp.Problem)
                .WithMany()
                .HasForeignKey(cp => cp.ProblemId);

            // Set default values for User statistics
            modelBuilder.Entity<User>()
                .Property(u => u.ProblemsSolved)
                .HasDefaultValue(0);

            modelBuilder.Entity<User>()
                .Property(u => u.ContestsParticipated)
                .HasDefaultValue(0);

            modelBuilder.Entity<User>()
                .Property(u => u.TotalPoints)
                .HasDefaultValue(0);

            // Ensure other models with no generic IDs aren't mapped wrongly,
            // (e.g. ViewModels) - we simply don't add DbSets for ViewModels.
        }
    }
}
