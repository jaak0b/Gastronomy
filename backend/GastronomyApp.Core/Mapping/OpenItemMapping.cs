using GastronomyApp.Contracts.OpenItems;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Queries;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class OpenItemMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OrderItem, TableOrderRecordItemView>()
          .Map(view => view.OrderItemId, item => item.Id)
          .Map(view => view.OrderId, item => item.StationOrder.Order.Id)
          .Map(view => view.GlobalOrderNumber, item => item.StationOrder.Order.GlobalOrderNumber)
          .Map(view => view.OrderedAtUtc, item => item.StationOrder.Order.CreatedAtUtc)
          .Map(view => view.State, item => item.State())
          .Map(view => view.StateSetAtUtc, item => item.StateSetAtUtc())
          .Map(view => view.WasHandedOutWhenCancelled, item => item.WasHandedOutWhenCancelled());

    config.NewConfig<SearchedOrder, TableOrderRecordView>()
          .Map(view => view.OrderId, searched => searched.Order.Id)
          .Map(view => view.GlobalOrderNumber, searched => searched.Order.GlobalOrderNumber)
          .Map(view => view.CreatedAtUtc, searched => searched.Order.CreatedAtUtc)
          .Map(view => view.StaffMemberName, searched => searched.Order.StaffMember.Name)
          .Map(view => view.NotCancelledItemCount, searched => searched.Order.Items().Count(item => item.IsStillOrdered()))
          .Map(view => view.HandedOutItemCount, searched => searched.Order.Items().Count(item => item.IsHandedOut()))
          .Map(view => view.Items, searched => searched.ShownItems.OrderBy(item => item.ItemName, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Note, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Id).ToList());

    config.NewConfig<OrderItem, OrderItemHistoryView>()
          .Map(view => view.OrderedByStaffMemberName, item => item.StationOrder.Order.StaffMember.Name)
          .Map(view => view.OrderedAtUtc, item => item.StationOrder.Order.CreatedAtUtc)
          .Map(view => view.StatusChanges, item => item.Statuses.OrderBy(status => status.CreatedAtUtc))
          .Map(view => view.SettledAtUtc, item => item.SettledAtUtc)
          .Map(view => view.ChargedPriceCents, item => item.ChargedPriceCents)
          .Map(view => view.PaymentMethod, item => item.PaymentMethod)
          .Map(view => view.SettledByStaffMemberName, item => item.SettledByStaffMember!.Name)
          .Map(view => view.PaymentNotice, item => item.PaymentNotice);

    config.NewConfig<OrderItemStatus, OrderItemStatusChangeView>()
          .Map(view => view.State, status => status.Status)
          .Map(view => view.ChangedAtUtc, status => status.CreatedAtUtc)
          .Map(view => view.StaffMemberName, status => status.CreatedByStaffMember!.Name)
          .Map(view => view.StationName, status => status.CreatedByStation!.Name);

    config.NewConfig<SettlementResult, SettlementView>()
          .Map(view => view.SettledOrderItemIds, settlement => settlement.NewlySettled.Select(item => item.Id).ToList())
          .Map(view => view.ReappliedOrderItemIds, settlement => settlement.Reapplied.Select(item => item.Id).ToList())
          .Map(view => view.AlreadySettledByOthersOrderItemIds, settlement => settlement.AlreadySettledByOthers.Select(item => item.Id).ToList());
  }
}
