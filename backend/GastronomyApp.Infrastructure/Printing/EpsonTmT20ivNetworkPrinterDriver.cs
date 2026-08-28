using System.Net.Sockets;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Printing;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class EpsonTmT20ivNetworkPrinterDriver : PrinterDriver<EpsonTmT20ivNetworkPrinter>
{
    private readonly TimeProvider timeProvider;

    public EpsonTmT20ivNetworkPrinterDriver(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public override int CharactersPerLine => 48;

    public override string CodePageName => "PC858";

    public override TimeSpan ConnectTimeout => TimeSpan.FromSeconds(3);

    public override TimeSpan JobTimeout => TimeSpan.FromSeconds(90);

    public override TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(10);

    public override TimeSpan StatusQueryTimeout => TimeSpan.FromSeconds(3);

    protected override async Task<IPrinterSession> ConnectAsync(
        EpsonTmT20ivNetworkPrinter printer,
        CancellationToken cancellationToken)
    {
        TcpClient client = new();
        using CancellationTokenSource connectCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCancellation.CancelAfter(ConnectTimeout);

        try
        {
            await client.ConnectAsync(printer.Host, printer.Port, connectCancellation.Token);
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException)
        {
            client.Dispose();
            throw new PrinterUnreachableException(
                $"The printer at {printer.Host}:{printer.Port} could not be reached: {error.Message}",
                error);
        }

        EpsonTmT20ivNetworkPrinterSession session = new(
            client,
            new PrinterSessionTimeouts(JobTimeout, HeartbeatInterval, StatusQueryTimeout),
            timeProvider);
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
