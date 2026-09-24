using ErrorOr;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class EstimateService
{
  private readonly ICatalogItemRepository _catalogItemRepository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly IStationRepository _stationRepository;
  private readonly StationService _stationService;

  public EstimateService(IStationRepository stationRepository, ICatalogItemRepository catalogItemRepository, RunningFestivalLookup runningFestival, StationService stationService)
  {
    _stationRepository = stationRepository;
    _catalogItemRepository = catalogItemRepository;
    _runningFestival = runningFestival;
    _stationService = stationService;
  }

  public async Task<IReadOnlyList<ItemStationAssignment>> ReadTimedAssignmentsAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    return await _catalogItemRepository.FindTimedAssignmentsIncludingOpenItemsAsync(festival.Id, cancellationToken);
  }

  public async Task<ErrorOr<IReadOnlyList<StationQuote>>> QuoteAsync(EstimateQuoteRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var festival = await _runningFestival.FindAsync(cancellationToken);

    IReadOnlyList<Station> stations = festival is null ? [] : await _stationRepository.FindAtFestivalWithOpenItemsAsync(festival.Id, cancellationToken);

    IReadOnlyList<CatalogItem> catalogItems = await _catalogItemRepository.FindByIdsAsync(request.Lines.Select(line => line.CatalogItemId).Distinct().ToList(), cancellationToken);

    List<Error> refusals = request.Lines.Where(line => catalogItems.All(item => item.Id != line.CatalogItemId))
                                  .Select(line => Refusal.EstimateQuote.UnknownCatalogItem(line.CatalogItemId))
                                  .Concat(request.Lines.Where(line => stations.All(station => station.Id != line.StationId)).Select(line => Refusal.EstimateQuote.UnknownStation(line.StationId)))
                                  .ToList();

    if (refusals.Count != 0)
      return refusals;

    return request.Lines.Select(line => line.StationId)
                  .Distinct()
                  .Select(stationId => new StationQuote(stationId, _stationService.QuotedReadyInMinutesOf(stations.Single(station => station.Id == stationId), request.Lines, catalogItems)))
                  .ToList()
                  .ToErrorOr<IReadOnlyList<StationQuote>>();
  }
}
