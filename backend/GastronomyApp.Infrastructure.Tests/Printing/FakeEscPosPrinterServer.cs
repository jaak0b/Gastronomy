using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GastronomyApp.Infrastructure.Tests.Printing;

internal sealed record ProcessIdEchoScript(bool Enabled, TimeSpan AfterDelay, int? OverrideProcessId);

internal sealed class FakeEscPosPrinterServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly List<byte> receivedBytes = [];
    private readonly Dictionary<int, byte> dleEotResponses = [];
    private readonly CancellationTokenSource lifetime = new();
    private readonly Lock guard = new();

    private byte[]? asbOnConnect;
    private ProcessIdEchoScript processIdEcho = new(true, TimeSpan.Zero, null);
    private int? dropAfterBytes;
    private bool dropImmediately;
    private byte[]? burstOnProcessIdRequest;
    private int acceptedConnectionCount;
    private Task? acceptLoop;

    public FakeEscPosPrinterServer(int port)
    {
        listener = new TcpListener(IPAddress.Loopback, port);
    }

    public int Port
    {
        get { return ((IPEndPoint)listener.LocalEndpoint).Port; }
    }

    public int AcceptedConnectionCount
    {
        get
        {
            lock (guard)
            {
                return acceptedConnectionCount;
            }
        }
    }

    public IReadOnlyList<byte> ReceivedBytes
    {
        get
        {
            lock (guard)
            {
                return [.. receivedBytes];
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        listener.Start();
        acceptLoop = Task.Run(() => AcceptLoopAsync(lifetime.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public void ScriptAsbOnConnect(byte paperStatusMask, byte errorStatusMask)
    {
        lock (guard)
        {
            asbOnConnect = [0x14, errorStatusMask, paperStatusMask, 0x00];
        }
    }

    public void ScriptDleEotResponse(int n, byte statusByte)
    {
        lock (guard)
        {
            dleEotResponses[n] = statusByte;
        }
    }

    public void ScriptProcessIdEcho(TimeSpan afterDelay)
    {
        lock (guard)
        {
            processIdEcho = new ProcessIdEchoScript(true, afterDelay, null);
        }
    }

    public void ScriptProcessIdEchoCarrying(int processId)
    {
        lock (guard)
        {
            processIdEcho = new ProcessIdEchoScript(true, TimeSpan.Zero, processId);
        }
    }

    public void ScriptBurstOnProcessIdRequest(byte paperStatusMask, byte errorStatusMask, byte statusByte)
    {
        lock (guard)
        {
            burstOnProcessIdRequest = [0x14, errorStatusMask, paperStatusMask, 0x00, statusByte];
        }
    }

    public void ScriptNeverEchoProcessId()
    {
        lock (guard)
        {
            processIdEcho = new ProcessIdEchoScript(false, TimeSpan.Zero, null);
        }
    }

    public void ScriptDropConnectionAfterBytes(int byteCount)
    {
        lock (guard)
        {
            dropAfterBytes = byteCount;
        }
    }

    public void ScriptDropConnectionImmediately()
    {
        lock (guard)
        {
            dropImmediately = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync();
        listener.Stop();
        if (acceptLoop is not null)
        {
            await Task.WhenAny(acceptLoop, Task.Delay(500));
        }

        lifetime.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (Exception error) when (error is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            lock (guard)
            {
                acceptedConnectionCount++;
            }

            _ = Task.Run(() => ServeAsync(client, cancellationToken), CancellationToken.None);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using TcpClient owned = client;
        NetworkStream stream = owned.GetStream();

        if (ReadDropImmediately())
        {
            owned.Close();
            return;
        }

        byte[]? initialAsb = ReadAsbOnConnect();
        if (initialAsb is not null)
        {
            await stream.WriteAsync(initialAsb, cancellationToken);
        }

        byte[] buffer = new byte[4096];
        int totalRead = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, cancellationToken);
            }
            catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException)
            {
                return;
            }

            if (read == 0)
            {
                return;
            }

            byte[] chunk = buffer[..read];
            lock (guard)
            {
                receivedBytes.AddRange(chunk);
            }

            totalRead += read;
            int? dropAt = ReadDropAfterBytes();
            if (dropAt is not null && totalRead >= dropAt.Value)
            {
                owned.Close();
                return;
            }

            await RespondAsync(stream, chunk, cancellationToken);
        }
    }

    private async Task RespondAsync(NetworkStream stream, byte[] chunk, CancellationToken cancellationToken)
    {
        for (int index = 0; index < chunk.Length; index++)
        {
            if (index + 2 < chunk.Length && chunk[index] == 0x10 && chunk[index + 1] == 0x04)
            {
                byte? response = ReadDleEotResponse(chunk[index + 2]);
                if (response is not null)
                {
                    await stream.WriteAsync(new[] { response.Value }, cancellationToken);
                }

                continue;
            }

            if (index + 10 < chunk.Length
                && chunk[index] == 0x1D
                && chunk[index + 1] == 0x28
                && chunk[index + 2] == 0x48)
            {
                ProcessIdEchoScript script = ReadProcessIdEcho();
                byte[]? burst = ReadBurst();
                if (burst is not null)
                {
                    await stream.WriteAsync(burst, cancellationToken);
                }

                if (!script.Enabled)
                {
                    continue;
                }

                byte[] processId = script.OverrideProcessId is null
                    ? chunk[(index + 7)..(index + 11)]
                    : Encoding.ASCII.GetBytes(script.OverrideProcessId.Value.ToString("D4", CultureInfo.InvariantCulture));
                if (script.AfterDelay > TimeSpan.Zero)
                {
                    await Task.Delay(script.AfterDelay, cancellationToken);
                }

                byte[] echo = [0x37, 0x22, .. processId, 0x00];
                await stream.WriteAsync(echo, cancellationToken);
            }
        }
    }

    private byte[]? ReadBurst()
    {
        lock (guard)
        {
            return burstOnProcessIdRequest;
        }
    }

    private bool ReadDropImmediately()
    {
        lock (guard)
        {
            return dropImmediately;
        }
    }

    private byte[]? ReadAsbOnConnect()
    {
        lock (guard)
        {
            return asbOnConnect;
        }
    }

    private int? ReadDropAfterBytes()
    {
        lock (guard)
        {
            return dropAfterBytes;
        }
    }

    private byte? ReadDleEotResponse(byte n)
    {
        lock (guard)
        {
            return dleEotResponses.TryGetValue(n, out byte response) ? response : null;
        }
    }

    private ProcessIdEchoScript ReadProcessIdEcho()
    {
        lock (guard)
        {
            return processIdEcho;
        }
    }
}
