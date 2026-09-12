using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class FestivalSchedule
{
  public bool IsRunning(Festival festival, DateTime nowUtc)
  {
    ArgumentNullException.ThrowIfNull(festival);

    return !festival.IsHidden && nowUtc >= festival.StartsAtUtc && nowUtc < festival.EndsAtUtc;
  }

  public Festival? RunningAt(IReadOnlyCollection<Festival> festivals, DateTime nowUtc)
  {
    ArgumentNullException.ThrowIfNull(festivals);

    return festivals.FirstOrDefault(festival => IsRunning(festival, nowUtc));
  }

  public bool HasStartWithin(IReadOnlyCollection<Festival> festivals, DateTime nowUtc, TimeSpan window)
  {
    ArgumentNullException.ThrowIfNull(festivals);

    return festivals.Any(festival => festival.StartsAtUtc > nowUtc
                                     && festival.StartsAtUtc <= nowUtc + window);
  }

  public Festival? Overlapping(Guid candidateId,
                               DateTime startsAtUtc,
                               DateTime endsAtUtc,
                               IReadOnlyCollection<Festival> others)
  {
    ArgumentNullException.ThrowIfNull(others);

    return others.FirstOrDefault(other => other.Id != candidateId
                                          && startsAtUtc < other.EndsAtUtc
                                          && other.StartsAtUtc < endsAtUtc);
  }
}
