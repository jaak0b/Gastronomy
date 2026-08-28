using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Printing;

namespace GastronomyApp.Core.Tests.Printing;

public static class APrinter
{
  public static TestPrinter Test()
  {
    return new() { Id = Guid.NewGuid(), Name = "Testdrucker" };
  }

  public static EpsonTmT20ivNetworkPrinter Network()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = "Drucker Küche",
             Host = "192.168.1.30",
             Port = 9100
           };
  }
}

public sealed class TestPrinterDriverDouble : PrinterDriver<TestPrinter>
{
  public TestPrinter? LastPrinter { get; private set; }

  override public int CharactersPerLine => 42;

  override public string CodePageName => "CP858";

  override public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(5);

  override public TimeSpan JobTimeout => TimeSpan.FromSeconds(90);

  override public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(10);

  override public TimeSpan StatusQueryTimeout => TimeSpan.FromSeconds(3);

  override protected Task<IPrinterSession> ConnectAsync(TestPrinter printer, CancellationToken cancellationToken)
  {
    LastPrinter = printer;
    return Task.FromResult<IPrinterSession>(null!);
  }
}

public sealed class NetworkPrinterDriverDouble : PrinterDriver<EpsonTmT20ivNetworkPrinter>
{
  override public int CharactersPerLine => 42;

  override public string CodePageName => "CP858";

  override public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(5);

  override public TimeSpan JobTimeout => TimeSpan.FromSeconds(90);

  override public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(10);

  override public TimeSpan StatusQueryTimeout => TimeSpan.FromSeconds(3);

  override protected Task<IPrinterSession> ConnectAsync(EpsonTmT20ivNetworkPrinter printer,
                                                        CancellationToken cancellationToken)
  {
    return Task.FromResult<IPrinterSession>(null!);
  }
}
