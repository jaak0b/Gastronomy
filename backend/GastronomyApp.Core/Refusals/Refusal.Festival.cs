using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class Festival
  {
    public static Error FestivalNotFound(Guid festivalId)
    {
      return NotFound("FestivalNotFound",
                      $"The festival {festivalId} does not exist.");
    }

    public static Error PeriodInvalid(DateTime startsAtUtc, DateTime endsAtUtc)
    {
      return BadRequest("admin.festivalPeriodInvalid",
                        $"The festival is to end at {endsAtUtc:O}, which is not after its start at {startsAtUtc:O}.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }

    public static Error PeriodOverlapsAnotherFestival(string overlappingFestivalName)
    {
      return Conflict("admin.festivalOverlaps",
                      $"The period overlaps the festival {overlappingFestivalName}.",
                      new Dictionary<string, object>
                      {
                        [MetadataKeys.ProblemCode] = "FestivalOverlaps",
                        [MetadataKeys.Name] = overlappingFestivalName
                      });
    }

    public static Error FestivalIsRunning(Guid festivalId)
    {
      return BadRequest("admin.actionFailed",
                        $"The festival {festivalId} was not hidden because it is running right now, and hiding it would empty every phone and every station tablet in the middle of service. The festivals page draws no hide control on a running festival, so this call did not come from that screen.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }
  }
}
