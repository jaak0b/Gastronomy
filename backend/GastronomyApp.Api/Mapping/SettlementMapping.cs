using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class SettlementMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<SettlementResult, SettlementView>()
          .Map(view => view.SettledOrderItemIds, settlement => IdsOf(settlement.NewlySettled))
          .Map(view => view.ReappliedOrderItemIds, settlement => IdsOf(settlement.Reapplied))
          .Map(view => view.AlreadySettledByOthersOrderItemIds, settlement => IdsOf(settlement.AlreadySettledByOthers));
  }

  private IReadOnlyList<Guid> IdsOf(IEnumerable<OrderItem> items)
  {
    return items.Select(item => item.Id).ToList();
  }
}
