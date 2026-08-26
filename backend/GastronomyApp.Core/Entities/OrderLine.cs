namespace GastronomyApp.Core.Entities;

public sealed class OrderLine
{
    public required Guid Id { get; set; }
    public required Guid OrderId { get; set; }
    public required Guid LocationTicketId { get; set; }
    public required Guid CatalogItemId { get; set; }
    public Guid? ChosenProductionLocationId { get; set; }
    public required string ItemNameSnapshot { get; set; }
    public required int UnitPriceCentsSnapshot { get; set; }
    public required int Quantity { get; set; }
    public string? Note { get; set; }
}
