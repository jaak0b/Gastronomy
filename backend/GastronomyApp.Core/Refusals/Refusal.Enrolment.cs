using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Enrolment
  {
    public static Error AtMostOneOwner()
    {
      return BadRequest("enrolment.atMostOneOwner",
                        "The invitation names a waiter and a station at once, and a device belongs to one owner.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }

    public static Error OwnerNotFound()
    {
      return NotFound("OwnerNotFound",
                      "The waiter or station the invitation names does not exist.");
    }

    public static Error InvitationUnknown(Guid invitationId)
    {
      return NotFound("admin.enrol.qrUnavailable",
                      $"The enrolment invitation {invitationId} does not exist.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "EnrolmentCodeUnknown"
                      });
    }

    public static Error InvitationReplaced(Guid invitationId)
    {
      return Gone("admin.enrol.qrReplaced",
                  $"The enrolment invitation {invitationId} was replaced by a newer one.",
                  new Dictionary<string, object>
                  {
                    [MetadataKeys.ProblemCode] = "EnrolmentCodeReplaced"
                  });
    }

    public static Error InvitationAlreadyUsed(Guid invitationId)
    {
      return Gone("admin.enrol.qrAlreadyUsed",
                  $"The enrolment invitation {invitationId} was already scanned by a device.",
                  new Dictionary<string, object>
                  {
                    [MetadataKeys.ProblemCode] = "EnrolmentCodeAlreadyUsed"
                  });
    }

    public static Error InvitationExpired(Guid invitationId)
    {
      return Gone("admin.enrol.expired",
                  $"The enrolment invitation {invitationId} ran out before it was scanned.",
                  new Dictionary<string, object>
                  {
                    [MetadataKeys.ProblemCode] = "EnrolmentCodeExpired"
                  });
    }
  }
}
