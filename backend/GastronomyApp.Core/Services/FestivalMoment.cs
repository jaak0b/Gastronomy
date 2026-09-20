namespace GastronomyApp.Core.Services;

public sealed class FestivalMoment
{
  public DateTime AsUtc(DateTime moment)
  {
    return moment.Kind == DateTimeKind.Local
             ? moment.ToUniversalTime()
             : DateTime.SpecifyKind(moment, DateTimeKind.Utc);
  }
}
