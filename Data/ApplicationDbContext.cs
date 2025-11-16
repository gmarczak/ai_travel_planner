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
    public DbSet<DestinationImage> DestinationImages { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Ignore pending model changes warning to allow migrations to run in production
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Identity string key columns for SQL Server compatibility
        // SQL Server cannot use nvarchar(max) in indexes/primary keys, so we limit to 450
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.Id).HasMaxLength(450);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>(entity =>
        {
            entity.Property(e => e.Id).HasMaxLength(450);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450);
            entity.Property(e => e.RoleId).HasMaxLength(450);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>(entity =>
        {
            entity.Property(e => e.UserId).HasMaxLength(450);
        });

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>(entity =>
        {
            entity.Property(e => e.RoleId).HasMaxLength(450);
        });

        // Configure TravelPlan entity
        builder.Entity<TravelPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Destination).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Budget).HasPrecision(18, 2); // Fix SQL Server decimal warning
            entity.Property(e => e.GeneratedItinerary).HasMaxLength(10000);
            entity.Property(e => e.ExternalId).HasMaxLength(100);

        // Foreign key to ApplicationUser (align navigation to avoid shadow FKs)
        entity.HasOne(e => e.User)
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

        // Configure DestinationImage entity (cache for Unsplash images)
        builder.Entity<DestinationImage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Destination).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ImageUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PhotographerName).HasMaxLength(200);
            entity.Property(e => e.PhotographerUrl).HasMaxLength(500);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.HasIndex(e => e.Destination).IsUnique();
            entity.HasIndex(e => e.CachedAt);
        });
    }
}
