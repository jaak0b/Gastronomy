namespace GastronomyApp.Api.Tests.Printing;

public sealed class TestTimeProvider : TimeProvider
{
  private DateTimeOffset _now;

  public TestTimeProvider(DateTimeOffset start)
  {
    _now = start;
  }

  override public DateTimeOffset GetUtcNow()
  {
    return _now;
  }

  public void Advance(TimeSpan amount)
  {
    _now += amount;
  }
}
