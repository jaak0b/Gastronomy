using GastronomyApp.Desktop.Values;

namespace GastronomyApp.Desktop.Ports;

public interface INetshCommand
{
  public NetshResult Run(string arguments);
}
