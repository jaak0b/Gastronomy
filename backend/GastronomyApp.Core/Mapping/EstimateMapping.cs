using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class EstimateMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<CatalogItem, ArticleTiming>().Map(timing => timing.CatalogItemId, item => item.Id);

    config.NewConfig<ItemStationAssignment, TimedAssignment>().Map(timed => timed.Article, assignment => assignment.CatalogItem);

    config.NewConfig<OrderItem, OpenWork>().Map(work => work.StationId, item => item.StationOrder.StationId).Map(work => work.ProductionMinutes, item => item.CatalogItem.ProductionMinutes ?? 0).Map(work => work.IsQueueIndependent, item => item.CatalogItem.IsQueueIndependent);
  }
}
