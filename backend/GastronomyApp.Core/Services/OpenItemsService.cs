using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class OpenItemsService
{
  private readonly IOpenItemRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;

  public OpenItemsService(IOpenItemRepository repository, RunningFestivalLookup runningFestival)
  {
    _repository = repository;
    _runningFestival = runningFestival;
  }

  public async Task<ErrorOr<Table>> ReadTableWithOrdersAsync(Guid tableId, CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return Refusal.Settlement.NoRunningFestival();

    var table = await _repository.FindTableWithOrdersAsync(festival.Id, tableId, cancellationToken);

    if (table is null)
      return Refusal.Settlement.TableNotAtTheRunningFestival(tableId);

    return table;
  }
}
