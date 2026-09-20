using GastronomyApp.Core.Entities;

namespace GastronomyApp.Desktop.Hosting;

public interface IFestivalReader
{
  public Task<IReadOnlyCollection<Festival>> ReadAllAsync(CancellationToken cancellationToken);
}
