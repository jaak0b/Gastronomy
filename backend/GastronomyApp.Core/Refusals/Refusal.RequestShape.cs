using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class RequestShape
  {
    public static Error MemberRefused(string member, string messageKey)
    {
      return BadRequest(messageKey,
                        $"The member {member} of the request does not match the contract.",
                        new()
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed,
                          [MetadataKeys.Member] = member
                        });
    }
  }
}
