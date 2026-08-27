using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GastronomyApp.Infrastructure;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GastronomyAppDbContext>
{
    public GastronomyAppDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<GastronomyAppDbContext> options = new DbContextOptionsBuilder<GastronomyAppDbContext>()
            .UseSqlite("Data Source=gastronomyapp-design-time.db")
            .Options;

        return new GastronomyAppDbContext(options);
    }
}
