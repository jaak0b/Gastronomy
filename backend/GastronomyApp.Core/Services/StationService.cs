using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class StationService
{
  public bool HasOutstandingInvitation(Station station)
  {
    ArgumentNullException.ThrowIfNull(station);

    return station.EnrolmentInvitation is not null;
  }

  public bool IsAtAnyFestival(Station station)
  {
    ArgumentNullException.ThrowIfNull(station);

    return station.FestivalStations.Count != 0;
  }

  public double QueuedMinutesOf(Station station)
  {
    ArgumentNullException.ThrowIfNull(station);

    return Math.Round(station.StationOrders.SelectMany(stationOrder => stationOrder.Items).Where(item => item.FulfilledAtUtc == null && !item.CatalogItem.IsQueueIndependent).Sum(item => item.CatalogItem.ProductionMinutes ?? 0), 1);
  }

  public double OpenMinutesOfArticle(Station station, Guid catalogItemId)
  {
    ArgumentNullException.ThrowIfNull(station);

    return Math.Round(station.StationOrders.SelectMany(stationOrder => stationOrder.Items).Where(item => item.FulfilledAtUtc == null && item.CatalogItemId == catalogItemId).Sum(item => item.CatalogItem.ProductionMinutes ?? 0), 1);
  }

  public double ReadyInMinutesOf(ItemStationAssignment assignment)
  {
    ArgumentNullException.ThrowIfNull(assignment);

    var queuedMinutes = assignment.CatalogItem.IsQueueIndependent ? OpenMinutesOfArticle(assignment.Station, assignment.CatalogItemId) : QueuedMinutesOf(assignment.Station);

    return Math.Round(queuedMinutes + (assignment.CatalogItem.ProductionMinutes ?? 0), 1);
  }

  public double? QuotedReadyInMinutesOf(Station station, IEnumerable<EstimateQuoteLine> lines, IEnumerable<CatalogItem> catalogItems)
  {
    ArgumentNullException.ThrowIfNull(station);
    ArgumentNullException.ThrowIfNull(lines);
    ArgumentNullException.ThrowIfNull(catalogItems);

    var linesAtStation = lines.Where(line => line.StationId == station.Id)
                              .Join(catalogItems, line => line.CatalogItemId, article => article.Id, (line, article) => new { line.Units, Article = article })
                              .ToList();

    if (linesAtStation.All(line => line.Article.ProductionMinutes == null))
      return null;

    IEnumerable<double> sharedLanes = linesAtStation.Any(line => !line.Article.IsQueueIndependent) ? [QueuedMinutesOf(station) + linesAtStation.Where(line => !line.Article.IsQueueIndependent).Sum(line => line.Units * (line.Article.ProductionMinutes ?? 0))] : [];

    var articleLanes = linesAtStation.Where(line => line.Article.IsQueueIndependent)
                                     .GroupBy(line => line.Article)
                                     .Select(articleLines => OpenMinutesOfArticle(station, articleLines.Key.Id) + articleLines.Sum(line => line.Units * (line.Article.ProductionMinutes ?? 0)));

    return Math.Round(sharedLanes.Concat(articleLanes).Max(), 1);
  }
}
