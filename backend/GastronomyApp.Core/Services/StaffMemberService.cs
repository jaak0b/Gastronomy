using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class StaffMemberService
{
  public bool HasOutstandingInvitation(StaffMember staffMember)
  {
    ArgumentNullException.ThrowIfNull(staffMember);

    return staffMember.EnrolmentInvitation is not null;
  }
}
