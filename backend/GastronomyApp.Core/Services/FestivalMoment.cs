namespace GastronomyApp.Core.Services;

public sealed class FestivalMoment
{
  public DateTime AsUtc(DateTime moment)
  {
    if (moment.Kind == DateTimeKind.Local)
      return moment.ToUniversalTime();

    return DateTime.SpecifyKind(moment, DateTimeKind.Utc);
  }
}
