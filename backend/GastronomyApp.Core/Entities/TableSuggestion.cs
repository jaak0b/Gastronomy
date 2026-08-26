namespace GastronomyApp.Core.Entities;

public sealed class TableSuggestion
{
    public required Guid Id { get; set; }
    public required string Label { get; set; }
    public required int SortOrder { get; set; }
}
