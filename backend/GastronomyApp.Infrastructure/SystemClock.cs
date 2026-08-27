using GastronomyApp.Core.Ports;

namespace GastronomyApp.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
