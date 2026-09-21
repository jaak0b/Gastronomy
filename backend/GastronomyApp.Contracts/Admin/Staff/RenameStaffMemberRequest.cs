namespace GastronomyApp.Contracts.Admin.Staff;

public sealed record RenameStaffMemberRequest
{
  public required string? Name { get; init; }
}
