namespace GastronomyApp.Core.Results;

public sealed record EnrolmentInvitationFailure
{
  public required EnrolmentInvitationFailureReason Reason { get; init; }
}
