using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using Mapster;

namespace GastronomyApp.Api.Endpoints;

public sealed class StationQueueMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<QueuedOrderItem, StationQueueItemView>();

    config.NewConfig<QueuedStationOrder, StationOrderQueueView>();

    config.NewConfig<StationQueue, StationSummaryView>().Map(view => view.Id, queue => queue.StationId).Map(view => view.Name, queue => queue.StationName);

    config.NewConfig<StationQueue, StationQueueView>().Map(view => view.Station, queue => queue).Map(view => view.AsItComes, queue => queue.AsItComesOrders);
  }
}
