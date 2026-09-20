namespace GastronomyApp.Core.Results;

public sealed record StaffMemberAdministrationFailure
{
  public required StaffMemberAdministrationFailureReason Reason { get; init; }
}
