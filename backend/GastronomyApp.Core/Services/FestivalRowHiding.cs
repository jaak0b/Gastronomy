using System.Linq.Expressions;
using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class FestivalRowHiding
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly RunningFestivalLookup _runningFestival;

  public FestivalRowHiding(IGastronomyDatabase database, TypeAdapterConfig mapping, RunningFestivalLookup runningFestival)
  {
    _database = database;
    _mapping = mapping;
    _runningFestival = runningFestival;
  }

  public async Task<ErrorOr<TView>> HideAsync<TEntity, TView>(Guid festivalId, Guid id, Expression<Func<OrderItem, bool>> itemOfRow, Func<Guid, Error> notFound, Func<Guid, Error> staysShown, CancellationToken cancellationToken) where TEntity : class, IHideable, IFestivalScoped
  {
    ArgumentNullException.ThrowIfNull(itemOfRow);
    ArgumentNullException.ThrowIfNull(notFound);
    ArgumentNullException.ThrowIfNull(staysShown);

    var row = await _database.Set<TEntity>().FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.FestivalId == festivalId, cancellationToken);

    if (row is null)
      return notFound(id);

    if (row.IsHidden)
      return row.Adapt<TView>(_mapping);

    var festival = await _database.Festivals.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == festivalId, cancellationToken);

    if (festival is null)
      return Refusal.TablePlan.FestivalNotFound(festivalId);

    if (_runningFestival.IsRunning(festival) && await _database.OrderItems.AsNoTracking().WhereToHandOutOrToSettle().AnyAsync(itemOfRow, cancellationToken))
      return staysShown(id);

    row.IsHidden = true;
    await _database.SaveChangesAsync(cancellationToken);

    return row.Adapt<TView>(_mapping);
  }
}
