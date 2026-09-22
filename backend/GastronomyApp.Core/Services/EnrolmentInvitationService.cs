using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class EnrolmentInvitationService
{
  public bool IsOutstandingAt(EnrolmentInvitation invitation, DateTime nowUtc)
  {
    ArgumentNullException.ThrowIfNull(invitation);

    return invitation.ConsumedAtUtc is null && invitation.ExpiresAtUtc > nowUtc;
  }
}
