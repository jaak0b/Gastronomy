namespace GastronomyApp.Core.Ports;

public interface IClock
{
    public DateTime UtcNow { get; }
}
