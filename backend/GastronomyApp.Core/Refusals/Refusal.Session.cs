using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Session
  {
    public static Error OwnerUnknown()
    {
      return Unauthorized("OwnerUnknown", "The waiter or station the device token names does not exist any more.");
    }
  }
}
