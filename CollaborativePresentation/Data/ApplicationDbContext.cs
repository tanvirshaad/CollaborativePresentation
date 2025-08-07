using CollaborativePresentation.Models;
using Microsoft.EntityFrameworkCore;

namespace CollaborativePresentation.Data
{
    public class ApplicationDbContext: DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options): base(options)
        {
        }

        public DbSet<Presentation> Presentations { get; set; }
        public DbSet<Slide> Slides { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Presentation entity
            modelBuilder.Entity<Presentation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
                entity.Property(e => e.CreatorName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.CreatedAt).IsRequired();
            });

            // Configure Slide entity
            modelBuilder.Entity<Slide>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Order).IsRequired();
                entity.Property(e => e.LastModified).IsRequired();
                entity.HasOne(e => e.Presentation)
                      .WithMany(p => p.Slides)
                      .HasForeignKey(e => e.PresentationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Role).IsRequired();
                entity.HasOne(e => e.Presentation)
                      .WithMany(p => p.ConnectedUsers)
                      .HasForeignKey(e => e.PresentationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
