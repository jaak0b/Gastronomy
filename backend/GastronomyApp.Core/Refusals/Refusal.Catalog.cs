using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Catalog
  {
    public static Error NoRunningFestival()
    {
      return NotFound("NoRunningFestival", "No festival is running, so there is no menu to hand out.");
    }
  }
}
