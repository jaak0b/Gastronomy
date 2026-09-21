using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Admin.Staff;

public sealed record RenameStaffMemberRequest
{
  [RequiredText(ErrorMessage = RefusalMessageKeys.AdminStaffMemberNameMissing)]
  public required string? Name { get; init; }
}
