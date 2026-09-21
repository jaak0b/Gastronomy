using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Ports;

public interface IDeviceTokenSplitter
{
  public DeviceTokenParts? Split(string? presentedToken);
}
