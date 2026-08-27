using GastronomyApp.Core.Ports;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class AdjustableClock : IClock
{
    private DateTime _utcNow = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow => _utcNow;

    public void Advance(TimeSpan amount)
    {
        _utcNow += amount;
    }
}
