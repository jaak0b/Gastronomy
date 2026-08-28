namespace GastronomyApp.Api.Tests.Printing;

public sealed class TestTimeProvider : TimeProvider
{
  private DateTimeOffset now;

  public TestTimeProvider(DateTimeOffset start)
  {
    now = start;
  }

  override public DateTimeOffset GetUtcNow()
  {
    return now;
  }

  public void Advance(TimeSpan amount)
  {
    now += amount;
  }
}
