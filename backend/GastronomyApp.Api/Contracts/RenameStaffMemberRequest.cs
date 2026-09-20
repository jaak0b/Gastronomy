namespace GastronomyApp.Api.Contracts;

public sealed record RenameStaffMemberRequest
{
  public required string? Name { get; init; }
}
