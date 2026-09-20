namespace GastronomyApp.Desktop.Values;

public abstract record UpdatePreparation
{
  public sealed record UpToDate : UpdatePreparation;

  public sealed record Ready(string Version) : UpdatePreparation;

  public sealed record Failed(string FailureDetail) : UpdatePreparation;
}
