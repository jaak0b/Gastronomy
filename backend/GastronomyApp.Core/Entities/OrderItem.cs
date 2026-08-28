namespace GastronomyApp.Core.Entities;

public sealed class OrderItem
{
    public required Guid Id { get; set; }
    public required Guid StationOrderId { get; set; }
    public required Guid CatalogItemId { get; set; }
    public required string ItemName { get; set; }
    public required int UnitPriceCents { get; set; }
    public string? Note { get; set; }
}
