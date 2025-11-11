using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace project.Data;

/// <summary>
/// Design-time factory for ApplicationDbContext to ensure SQL Server is used for migrations
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        // Use a dummy SQL Server connection string for design-time migrations
        // The actual connection string will be used at runtime
        optionsBuilder.UseSqlServer("Server=.;Database=DesignTimeDb;Integrated Security=true");
        
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
