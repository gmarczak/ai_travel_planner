using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace project.Data;

/// <summary>
/// Design-time factory for ApplicationDbContext for migrations
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Use SQLite for design-time migrations
        optionsBuilder.UseSqlite("Data Source=travelplanner.db");

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
