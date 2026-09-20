namespace GastronomyApp.Core.ReadModels;

public sealed record OrderItemOwner
{
  public required Guid OrderId { get; init; }

  public required string TableName { get; init; }

  public required int GlobalOrderNumber { get; init; }

  public required DateTime OrderedAtUtc { get; init; }
}
