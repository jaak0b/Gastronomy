using GastronomyApp.Core.Ports;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class AdjustableClock : IClock
{

  public DateTime UtcNow { get; private set; } = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  public void Advance(TimeSpan amount)
  {
    UtcNow += amount;
  }
}
