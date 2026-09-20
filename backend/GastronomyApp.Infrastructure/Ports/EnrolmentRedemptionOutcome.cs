namespace GastronomyApp.Infrastructure.Ports;

public enum EnrolmentRedemptionOutcome
{
  Redeemed,
  CodeInvalid,
  CodeExpired,
  StaffMemberIsOffTheList,
  StationIsOffTheList,
  NameRequired,
  NoInvitationOutstanding
}
