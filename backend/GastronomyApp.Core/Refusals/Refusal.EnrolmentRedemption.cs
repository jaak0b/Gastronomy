using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class EnrolmentRedemption
  {
    public static Error NoInvitationOutstanding()
    {
      return Gone("enrolment.codeNoLongerValid",
                  "No invitation is outstanding, so the last one was already used or was replaced by a newer one.",
                  new Dictionary<string, object>
                  {
                    [MetadataKeys.ProblemCode] = "EnrolmentCodeNoLongerValid"
                  });
    }

    public static Error CodeInvalid(Guid invitationId)
    {
      return NotFound("enrolment.codeUnknown",
                      $"The code the device sent does not match the outstanding invitation {invitationId}.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "EnrolmentCodeUnknown"
                      });
    }

    public static Error CodeExpired(Guid invitationId)
    {
      return Gone("enrolment.codeNoLongerValid",
                  $"The outstanding invitation {invitationId} had already expired when the device scanned it.",
                  new Dictionary<string, object>
                  {
                    [MetadataKeys.ProblemCode] = "EnrolmentCodeNoLongerValid"
                  });
    }

    public static Error StaffMemberIsOffTheList(Guid invitationId)
    {
      return Gone("enrolment.staffMemberIsOffTheList",
                  $"The waiter the invitation {invitationId} names is off the list.",
                  new Dictionary<string, object>
                  {
                    [MetadataKeys.ProblemCode] = "StaffMemberIsOffTheList"
                  });
    }

    public static Error StationIsOffTheList(Guid invitationId)
    {
      return Gone("enrolment.stationIsOffTheList",
                  $"The station the invitation {invitationId} names is switched off.",
                  new Dictionary<string, object>
                  {
                    [MetadataKeys.ProblemCode] = "StationIsOffTheList"
                  });
    }

    public static Error NameRequired(Guid invitationId)
    {
      return BadRequest("enrolment.nameMissing",
                        $"The invitation {invitationId} names nobody and the phone sent no name.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }
  }
}
