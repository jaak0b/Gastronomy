namespace GastronomyApp.Core.Entities;

public sealed class ItemLocationAssignment
{
    public required Guid Id { get; set; }
    public required Guid CatalogItemId { get; set; }
    public required Guid ProductionLocationId { get; set; }
}
