using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;

namespace GastronomyApp.Infrastructure.Projections;

public sealed class OpenItemProjection : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OrderItemOwnerRow, OrderItemOwner>()
          .Map(owner => owner.OrderId, row => row.Order.Id)
          .Map(owner => owner.TableName, row => row.Order.TableName)
          .Map(owner => owner.GlobalOrderNumber, row => row.Order.GlobalOrderNumber)
          .Map(owner => owner.OrderedAtUtc, row => row.Order.CreatedAtUtc);
  }
}
