namespace GastronomyApp.Contracts;

public sealed record RenameStaffMemberRequest
{
  public required string? Name { get; init; }
}
