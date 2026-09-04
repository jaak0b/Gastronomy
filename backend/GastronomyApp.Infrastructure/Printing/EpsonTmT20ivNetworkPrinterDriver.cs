using System.Net.Sockets;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Printing;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class EpsonTmT20ivNetworkPrinterDriver : PrinterDriver<EpsonTmT20ivNetworkPrinter>
{
  private readonly TimeProvider _timeProvider;

  public EpsonTmT20ivNetworkPrinterDriver(TimeProvider timeProvider)
  {
    _timeProvider = timeProvider;
  }

  override public int CharactersPerLine => 48;

  override public string CodePageName => "PC858";

  override public TimeSpan ConnectTimeout => TimeSpan.FromSeconds(3);

  override public TimeSpan JobTimeout => TimeSpan.FromSeconds(90);

  override public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(10);

  override public TimeSpan StatusQueryTimeout => TimeSpan.FromSeconds(3);

  override protected async Task<IPrinterSession> ConnectAsync(EpsonTmT20ivNetworkPrinter printer,
                                                              CancellationToken cancellationToken)
  {
    TcpClient client = new();
    using var connectCancellation =
      CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    connectCancellation.CancelAfter(ConnectTimeout);

    try
    {
      await client.ConnectAsync(printer.Host, printer.Port, connectCancellation.Token);
    }
    catch (Exception error) when (error is SocketException or OperationCanceledException)
    {
      client.Dispose();
      throw new PrinterUnreachableException($"The printer at {printer.Host}:{printer.Port} could not be reached: {error.Message}",
                                            error);
    }

    EpsonTmT20ivNetworkPrinterSession session = new(client,
                                                    new(JobTimeout, HeartbeatInterval, StatusQueryTimeout),
                                                    _timeProvider);
    await session.StartAsync();
    return session;
  }
}

public sealed class PrinterUnreachableException : Exception
{
  public PrinterUnreachableException()
  {
  }

  public PrinterUnreachableException(string message)
    : base(message)
  {
  }

  public PrinterUnreachableException(string message, Exception innerException)
    : base(message, innerException)
  {
  }
}
