using System.ComponentModel.DataAnnotations;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.OpenItems;

public sealed record TableOrderSearchRequest
{
  [EnumDataType(typeof(OrderStatus), ErrorMessage = RefusalMessageKeys.SettlementCannotBeProcessed)]
  public OrderStatus? OrderStatus { get; init; }

  [EnumDataType(typeof(PaymentStatus), ErrorMessage = RefusalMessageKeys.SettlementCannotBeProcessed)]
  public PaymentStatus? PaymentStatus { get; init; }

  public bool? IncludeCancelled { get; init; }
}
