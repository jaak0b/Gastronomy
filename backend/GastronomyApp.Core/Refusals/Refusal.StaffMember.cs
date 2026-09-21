using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class StaffMember
  {
    public static Error StaffMemberNotFound(Guid staffMemberId)
    {
      return NotFound("StaffMemberNotFound",
                      $"The waiter {staffMemberId} does not exist.");
    }
  }
}
