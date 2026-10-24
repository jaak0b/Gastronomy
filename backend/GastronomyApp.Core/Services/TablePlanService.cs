using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class TablePlanService
{
  private readonly ITablePlanRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;

  public TablePlanService(ITablePlanRepository repository, RunningFestivalLookup runningFestival)
  {
    _repository = repository;
    _runningFestival = runningFestival;
  }

  public async Task<IReadOnlyList<ShownArea>> ReadRunningFestivalTablePlanAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    return await _repository.FindShownAreasWithItemFlagsAsync(festival.Id, cancellationToken);
  }
}
