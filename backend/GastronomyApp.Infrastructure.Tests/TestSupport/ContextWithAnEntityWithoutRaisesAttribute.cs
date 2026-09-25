using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class ContextWithAnEntityWithoutRaisesAttribute : DbContext
{
  public ContextWithAnEntityWithoutRaisesAttribute(DbContextOptions<ContextWithAnEntityWithoutRaisesAttribute> options) : base(options)
  {
  }

  public DbSet<EntityWithoutRaisesAttribute> Entities => Set<EntityWithoutRaisesAttribute>();
}
