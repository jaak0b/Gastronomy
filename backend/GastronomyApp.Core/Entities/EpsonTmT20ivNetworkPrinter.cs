namespace GastronomyApp.Core.Entities;

public sealed class EpsonTmT20ivNetworkPrinter : Printer
{
  public required string Host { get; set; }
  public required int Port { get; set; }
}
