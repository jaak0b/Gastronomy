namespace GastronomyApp.Core.Results;

public enum EnrolmentInvitationFailureReason
{
  AtMostOneOwner = 1,
  OwnerNotFound = 2,
  InvitationUnknown = 3,
  InvitationReplaced = 4,
  InvitationAlreadyUsed = 5,
  InvitationExpired = 6
}
