using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class SettlementMapping : IMappingRegistration
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<SettlementResult, SettlementView>()
          .Map(view => view.SettledOrderItemIds, settlement => settlement.NewlySettled.Select(item => item.Id).ToList())
          .Map(view => view.ReappliedOrderItemIds, settlement => settlement.Reapplied.Select(item => item.Id).ToList())
          .Map(view => view.AlreadySettledByOthersOrderItemIds, settlement => settlement.AlreadySettledByOthers.Select(item => item.Id).ToList());
  }
}
