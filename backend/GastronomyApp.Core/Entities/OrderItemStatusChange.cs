using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class OrderItemStatusChange
{
  public required Guid Id { get; set; }

  public required Guid OrderItemId { get; set; }

  public required ProductionStatus Status { get; set; }

  public required DateTime ChangedAtUtc { get; set; }
}
