using ErrorOr;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Storage
  {
    public static Error ConflictingChange()
    {
      return Conflict(RefusalMessageKeys.ReviewConflictingChange, "Another writer changed the rows this request had read, so nothing it asked for was stored.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.ConflictingChange });
    }

    public static Error DatabaseUnavailable()
    {
      return ServiceUnavailable(RefusalMessageKeys.ReviewSendFailedDatabase, "The database file could not be written, so nothing this request asked for was stored.", new() { [MetadataKeys.ProblemCode] = ProblemCodes.DatabaseUnavailable });
    }
  }
}
