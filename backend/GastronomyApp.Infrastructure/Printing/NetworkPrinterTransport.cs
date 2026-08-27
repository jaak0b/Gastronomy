using System.Net.Sockets;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;

namespace GastronomyApp.Infrastructure.Printing;

public sealed class NetworkPrinterTransport : IPrinterTransport
{
    private readonly TimeProvider timeProvider;

    public NetworkPrinterTransport(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public TransportKind Kind
    {
        get { return TransportKind.Network; }
    }

    public async Task<IPrinterSession> ConnectAsync(PrinterEndpoint endpoint, CancellationToken cancellationToken)
    {
        if (endpoint.Host is null)
        {
            throw new InvalidOperationException(
                $"Production location {endpoint.ProductionLocationId} has no host configured for a network printer.");
        }

        TcpClient client = new();
        using CancellationTokenSource connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCancellation.CancelAfter(endpoint.ConnectTimeout);

        try
        {
            await client.ConnectAsync(endpoint.Host, endpoint.Port, connectCancellation.Token);
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException)
        {
            client.Dispose();
            throw new PrinterUnreachableException(
                $"The printer at {endpoint.Host}:{endpoint.Port} could not be reached: {error.Message}",
                error);
        }

        NetworkPrinterSession session = new(client, endpoint, timeProvider);
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
