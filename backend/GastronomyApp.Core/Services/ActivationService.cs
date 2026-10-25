using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Mapster;
using Microsoft.EntityFrameworkCore;

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

  public async Task<ErrorOr<TView>> ShowAsync<TEntity, TView>(Guid id, Func<Guid, Error> notFound, CancellationToken cancellationToken) where TEntity : class, IHideable
  {
    return await ShowAsync<TEntity, TView>(async () => await _database.Set<TEntity>().FindAsync([id], cancellationToken), id, notFound, cancellationToken);
  }

  public async Task<ErrorOr<TView>> ShowAsync<TEntity, TView>(Guid festivalId, Guid id, Func<Guid, Error> notFound, CancellationToken cancellationToken) where TEntity : class, IHideable, IFestivalScoped
  {
    return await ShowAsync<TEntity, TView>(async () => await _database.Set<TEntity>().FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.FestivalId == festivalId, cancellationToken), id, notFound, cancellationToken);
  }

  private async Task<ErrorOr<TView>> ShowAsync<TEntity, TView>(Func<Task<TEntity?>> find, Guid id, Func<Guid, Error> notFound, CancellationToken cancellationToken) where TEntity : class, IHideable
  {
    ArgumentNullException.ThrowIfNull(notFound);

    var row = await find();

    if (row is null)
      return notFound(id);

    row.IsHidden = false;
    await _database.SaveChangesAsync(cancellationToken);

    return row.Adapt<TView>(_mapping);
  }
}
