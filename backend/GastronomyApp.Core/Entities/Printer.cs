namespace GastronomyApp.Core.Entities;

public abstract class Printer
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
}
