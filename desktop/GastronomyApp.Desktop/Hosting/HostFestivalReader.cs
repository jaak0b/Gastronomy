using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Desktop.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Desktop.Hosting;

public sealed class HostFestivalReader : IFestivalReader
{
  private readonly HostLauncher _launcher;

  public HostFestivalReader(HostLauncher launcher)
  {
    _launcher = launcher;
  }

  public async Task<IReadOnlyCollection<Festival>> ReadAllAsync(CancellationToken cancellationToken)
  {
    var application = _launcher.Application;

    if (application is null)
      throw new InvalidOperationException("The server is not running, so the festival list cannot be read.");

    using var scope = application.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<IGastronomyDatabase>();

    return await database.Festivals.AsNoTracking().OrderByDescending(festival => festival.StartsAtUtc).ToListAsync(cancellationToken);
  }
}
