using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Mapster;

namespace GastronomyApp.Core.Services;

public sealed class ActivationService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;

  public ActivationService(IGastronomyDatabase database, TypeAdapterConfig mapping)
  {
    _database = database;
    _mapping = mapping;
  }

  public async Task<ErrorOr<TView>> ActivateAsync<TEntity, TView>(Guid id, Func<Guid, Error> notFound, CancellationToken cancellationToken) where TEntity : class, IActivatable
  {
    ArgumentNullException.ThrowIfNull(notFound);

    var row = await _database.Set<TEntity>().FindAsync([id], cancellationToken);

    if (row is null)
      return notFound(id);

    row.IsActive = true;
    await _database.SaveChangesAsync(cancellationToken);

    return row.Adapt<TView>(_mapping);
  }
}
