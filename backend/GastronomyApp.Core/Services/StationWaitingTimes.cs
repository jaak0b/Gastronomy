using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class StationWaitingTimes
{
  public double QueuedMinutesOf(IEnumerable<OpenWork> openWorkAtStation)
  {
    ArgumentNullException.ThrowIfNull(openWorkAtStation);

    return Math.Round(openWorkAtStation.Where(work => !work.IsQueueIndependent).Sum(work => work.ProductionMinutes), 1);
  }

  public double OpenMinutesOfArticle(IEnumerable<OpenWork> openWorkAtStation, Guid catalogItemId)
  {
    ArgumentNullException.ThrowIfNull(openWorkAtStation);

    return Math.Round(openWorkAtStation.Where(work => work.CatalogItemId == catalogItemId).Sum(work => work.ProductionMinutes), 1);
  }

  public double ReadyInMinutesOf(ArticleTiming article, IEnumerable<OpenWork> openWorkAtStation)
  {
    ArgumentNullException.ThrowIfNull(article);
    ArgumentNullException.ThrowIfNull(openWorkAtStation);

    var openWork = openWorkAtStation.ToList();
    var queuedMinutes = article.IsQueueIndependent ? OpenMinutesOfArticle(openWork, article.CatalogItemId) : QueuedMinutesOf(openWork);

    return Math.Round(queuedMinutes + (article.ProductionMinutes ?? 0), 1);
  }

  public double? QuotedReadyInMinutesOf(IEnumerable<OpenWork> openWorkAtStation, IEnumerable<EstimateQuoteLine> linesAtStation, IEnumerable<ArticleTiming> articles)
  {
    ArgumentNullException.ThrowIfNull(openWorkAtStation);
    ArgumentNullException.ThrowIfNull(linesAtStation);
    ArgumentNullException.ThrowIfNull(articles);

    var openWork = openWorkAtStation.ToList();
    var quotedLines = linesAtStation.Join(articles,
                                          line => line.CatalogItemId,
                                          article => article.CatalogItemId,
                                          (line, article) => new
                                                             {
                                                               line.Units,
                                                               Article = article
                                                             })
                                    .ToList();

    if (quotedLines.All(line => line.Article.ProductionMinutes == null))
      return null;

    IEnumerable<double> sharedLanes = quotedLines.Any(line => !line.Article.IsQueueIndependent) ? [QueuedMinutesOf(openWork) + quotedLines.Where(line => !line.Article.IsQueueIndependent).Sum(line => line.Units * (line.Article.ProductionMinutes ?? 0))] : [];

    var articleLanes = quotedLines.Where(line => line.Article.IsQueueIndependent).GroupBy(line => line.Article.CatalogItemId).Select(articleLines => OpenMinutesOfArticle(openWork, articleLines.Key) + articleLines.Sum(line => line.Units * (line.Article.ProductionMinutes ?? 0)));

    return Math.Round(sharedLanes.Concat(articleLanes).Max(), 1);
  }
}
