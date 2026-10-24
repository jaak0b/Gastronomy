using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Festival
  {
    public static Error FestivalNotFound(Guid festivalId)
    {
      return NotFound("FestivalNotFound", $"The festival {festivalId} does not exist.");
    }

    public static Error PeriodInvalid(DateTime startsAtUtc, DateTime endsAtUtc)
    {
      return BadRequest("errors.admin.festivals.periodInvalid", $"The festival is to end at {endsAtUtc:O}, which is not after its start at {startsAtUtc:O}.", new Dictionary<string, object> { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error PeriodOverlapsAnotherFestival(string overlappingFestivalName)
    {
      return Conflict("errors.admin.festivals.overlaps",
                      $"The period overlaps the festival {overlappingFestivalName}.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "FestivalOverlaps",
                        [MetadataKeys.Name] = overlappingFestivalName
                      });
    }

    public static Error FestivalIsRunning(Guid festivalId)
    {
      return Conflict("errors.admin.festivals.runningFestivalStaysShown",
                      $"The festival {festivalId} was not hidden because it is running right now, and hiding it would empty every phone and every station tablet in the middle of service.",
                      new Dictionary<string, object> { [MetadataKeys.ProblemCode] = "RunningFestivalStaysShown" });
    }
  }
}
