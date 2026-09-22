namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class ProblemCodes
  {
    public const string ValidationFailed = "ValidationFailed";

    public const string UnprocessableEntity = "UnprocessableEntity";

    public const string ConflictingChange = "ConflictingChange";

    public const string DatabaseUnavailable = "DatabaseUnavailable";
  }
}
