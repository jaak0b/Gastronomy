using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Printing;

namespace GastronomyApp.Core.Tests.Printing;

public static class APrinter
{
  public static TestPrinter Test()
  {
    return new TestPrinter { Id = Guid.NewGuid(), Name = "Testdrucker" };
  }

  public static EpsonTmT20ivNetworkPrinter Network()
  {
    return new EpsonTmT20ivNetworkPrinter
    {
      Id = Guid.NewGuid(),
      Name = "Drucker Küche",
      Host = "192.168.1.30",
      Port = 9100,
    };
  }
}

public sealed class TestPrinterDriverDouble : PrinterDriver<TestPrinter>
{
  public TestPrinter? LastPrinter { get; private set; }

  public override int CharactersPerLine => 42;

  public override string CodePageName => "CP858";

  public override TimeSpan ConnectTimeout => TimeSpan.FromSeconds(5);

  public override TimeSpan JobTimeout => TimeSpan.FromSeconds(90);

  public override TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(10);

  public override TimeSpan StatusQueryTimeout => TimeSpan.FromSeconds(3);

  protected override Task<IPrinterSession> ConnectAsync(TestPrinter printer, CancellationToken cancellationToken)
  {
    LastPrinter = printer;
    return Task.FromResult<IPrinterSession>(null!);
  }
}

public sealed class NetworkPrinterDriverDouble : PrinterDriver<EpsonTmT20ivNetworkPrinter>
{
  public override int CharactersPerLine => 42;

  public override string CodePageName => "CP858";

  public override TimeSpan ConnectTimeout => TimeSpan.FromSeconds(5);

  public override TimeSpan JobTimeout => TimeSpan.FromSeconds(90);

  public override TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(10);

  public override TimeSpan StatusQueryTimeout => TimeSpan.FromSeconds(3);

  protected override Task<IPrinterSession> ConnectAsync(
      EpsonTmT20ivNetworkPrinter printer,
      CancellationToken cancellationToken)
  {
    return Task.FromResult<IPrinterSession>(null!);
  }
}
