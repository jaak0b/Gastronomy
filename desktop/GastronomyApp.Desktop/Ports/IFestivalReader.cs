using GastronomyApp.Core.Entities;

namespace GastronomyApp.Desktop.Ports;

public interface IFestivalReader
{
  public Task<IReadOnlyCollection<Festival>> ReadAllAsync(CancellationToken cancellationToken);
}
