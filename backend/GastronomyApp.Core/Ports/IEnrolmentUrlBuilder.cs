namespace GastronomyApp.Core.Ports;

public interface IEnrolmentUrlBuilder
{
  public string BuildEnrolmentUrl(string qrCodeValue);
}
