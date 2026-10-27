using ErrorOr;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Results;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class EstimateService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly StationWaitingTimes _stationWaitingTimes;

  public EstimateService(IGastronomyDatabase database, TypeAdapterConfig mapping, RunningFestivalLookup runningFestival, StationWaitingTimes stationWaitingTimes)
  {
    _database = database;
    _mapping = mapping;
    _runningFestival = runningFestival;
    _stationWaitingTimes = stationWaitingTimes;
  }

  public async Task<IReadOnlyList<ItemEstimateView>> ListAsync(CancellationToken cancellationToken)
  {
    var festival = await _runningFestival.FindAsync(cancellationToken);

    if (festival is null)
      return [];

    var assignments = await _database.ItemStationAssignments.AsNoTracking()
                                     .Where(assignment => assignment.FestivalId == festival.Id && assignment.CatalogItem.ProductionMinutes != null && assignment.Station.IsActive && assignment.Station.FestivalStations.Any(link => link.FestivalId == festival.Id))
                                     .OrderBy(assignment => assignment.CatalogItem.SortOrder)
                                     .ThenBy(assignment => assignment.Station.SortOrder)
                                     .ProjectToType<TimedAssignment>(_mapping)
                                     .ToListAsync(cancellationToken);
    var openWorkByStationId = (await FindOpenWorkAsync(festival.Id, cancellationToken)).ToLookup(work => work.StationId);

    return assignments.Select(assignment => new ItemEstimateView(assignment.Article.CatalogItemId, assignment.StationId, _stationWaitingTimes.ReadyInMinutesOf(assignment.Article, openWorkByStationId[assignment.StationId]))).ToList();
  }

  public async Task<ErrorOr<EstimateQuoteView>> QuoteAsync(EstimateQuoteRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var festival = await _runningFestival.FindAsync(cancellationToken);

    IReadOnlyList<Guid> stationIds = festival is null ? [] : await _database.FestivalStations.AsNoTracking().ActiveAt(festival.Id).Select(link => link.StationId).ToListAsync(cancellationToken);

    List<Guid> requestedItemIds = request.Lines.Select(line => line.CatalogItemId).Distinct().ToList();
    var articles = await _database.CatalogItems.AsNoTracking().Where(item => requestedItemIds.Contains(item.Id)).ProjectToType<ArticleTiming>(_mapping).ToListAsync(cancellationToken);

    List<Error> refusals = request.Lines.Where(line => articles.All(article => article.CatalogItemId != line.CatalogItemId))
                                  .Select(line => Refusal.EstimateQuote.UnknownCatalogItem(line.CatalogItemId))
                                  .Concat(request.Lines.Where(line => !stationIds.Contains(line.StationId)).Select(line => Refusal.EstimateQuote.UnknownStation(line.StationId)))
                                  .ToList();

    if (refusals.Count != 0)
      return refusals;

    IReadOnlyList<OpenWork> openWork = festival is null ? [] : await FindOpenWorkAsync(festival.Id, cancellationToken);
    var openWorkByStationId = openWork.ToLookup(work => work.StationId);

    return new EstimateQuoteView(request.Lines.Select(line => line.StationId).Distinct().Select(stationId => new StationQuoteView(stationId, _stationWaitingTimes.QuotedReadyInMinutesOf(openWorkByStationId[stationId], request.Lines.Where(line => line.StationId == stationId), articles))).ToList());
  }

  private async Task<IReadOnlyList<OpenWork>> FindOpenWorkAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _database.OrderItems.AsNoTracking().WhereOpen().WhereFestivalIs(festivalId).ProjectToType<OpenWork>(_mapping).ToListAsync(cancellationToken);
  }
}
