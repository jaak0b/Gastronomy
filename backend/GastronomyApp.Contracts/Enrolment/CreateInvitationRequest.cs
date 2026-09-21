namespace GastronomyApp.Contracts.Enrolment;

public sealed record CreateInvitationRequest
{
  public Guid? StaffMemberId { get; init; }

  public Guid? StationId { get; init; }
}
