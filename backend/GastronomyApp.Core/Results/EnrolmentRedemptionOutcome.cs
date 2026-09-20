namespace GastronomyApp.Core.Results;

public enum EnrolmentRedemptionOutcome
{
  NoInvitationOutstanding = 0,
  Redeemed = 1,
  CodeInvalid = 2,
  CodeExpired = 3,
  StaffMemberIsOffTheList = 4,
  StationIsOffTheList = 5,
  NameRequired = 6
}
