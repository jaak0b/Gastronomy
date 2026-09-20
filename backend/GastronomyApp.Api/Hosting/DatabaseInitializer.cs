using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Hosting;

public sealed class DatabaseInitializer
{
  public void Initialize(IServiceProvider services)
  {
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<GastronomyAppDbContext>();
    context.Database.Migrate();
  }
}
