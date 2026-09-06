using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class OrderItem
{
  public required Guid Id { get; set; }

  public required Guid StationOrderId { get; set; }

  public required Guid CatalogItemId { get; set; }

  public required string ItemName { get; set; }

  public required int UnitPriceCents { get; set; }

  public string? Note { get; set; }

  public ProductionStatus ProductionStatus { get; set; }

  public DateTime? SettledAtUtc { get; set; }

  public int? ChargedPriceCents { get; set; }

  public Guid? SettledByStaffMemberId { get; set; }

  public string? PaymentNotice { get; set; }

  public List<OrderItemStatusChange> StatusChanges { get; set; } = [];
}
