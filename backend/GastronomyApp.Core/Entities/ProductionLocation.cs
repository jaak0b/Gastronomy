namespace GastronomyApp.Core.Entities;

public sealed class ProductionLocation
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required string StationAccessKey { get; set; }
    public required string SlipLanguage { get; set; }
    public required int SortOrder { get; set; }
    public required bool IsActive { get; set; }
}
