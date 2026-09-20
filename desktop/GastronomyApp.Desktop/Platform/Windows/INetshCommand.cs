namespace GastronomyApp.Desktop.Platform.Windows;

public interface INetshCommand
{
  public NetshResult Run(string arguments);
}
