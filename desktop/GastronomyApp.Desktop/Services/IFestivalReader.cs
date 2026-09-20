using GastronomyApp.Core.Entities;

namespace GastronomyApp.Desktop.Services;

public interface IFestivalReader
{
  public Task<IReadOnlyCollection<Festival>> ReadAllAsync(CancellationToken cancellationToken);
}
