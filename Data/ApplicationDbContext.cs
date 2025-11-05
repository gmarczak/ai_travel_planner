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
    public DbSet<AiResponseCache> AiResponseCaches { get; set; } = null!;
    public DbSet<PlanGenerationState> PlanGenerationStates { get; set; } = null!;

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

        // Configure AiResponseCache entity
        builder.Entity<AiResponseCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PromptHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Response).IsRequired();
            entity.HasIndex(e => e.PromptHash).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ExpiresAt);
        });

        // Configure PlanGenerationState entity
        builder.Entity<PlanGenerationState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExternalId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Destination).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CurrentStep).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(e => e.ExternalId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AnonymousCookieId);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
