namespace GastronomyApp.Api.Hub;

public sealed record HubEventNames
{
  public string OrderStatusChanged { get; } = "OrderStatusChanged";

  public string OrderItemsSettled { get; } = "OrderItemsSettled";

  public string StationOrdersChanged { get; } = "StationOrdersChanged";

  public string StationsChanged { get; } = "StationsChanged";

  public string FestivalChanged { get; } = "FestivalChanged";

  public string CatalogChanged { get; } = "CatalogChanged";

  public string EnrolmentCompleted { get; } = "EnrolmentCompleted";

  public string DeviceRevoked { get; } = "DeviceRevoked";
}
