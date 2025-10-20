using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using project.Models;

namespace project.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TravelPlan> TravelPlans { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure TravelPlan entity
        builder.Entity<TravelPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Destination).IsRequired().HasMaxLength(200);
            entity.Property(e => e.GeneratedItinerary).HasMaxLength(10000);
            entity.Property(e => e.ExternalId).HasMaxLength(100);

            // Foreign key to ApplicationUser
            entity.HasOne<ApplicationUser>()
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
