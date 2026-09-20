namespace GastronomyApp.Desktop.Services.Windows;

public interface INetshCommand
{
  public NetshResult Run(string arguments);
}
