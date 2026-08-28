using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Printing;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Infrastructure.Printing;

public sealed record PendingProcessIdEcho(int ProcessId, TaskCompletionSource<bool> Completion);

public enum InboundFrame
{
    Incomplete,
    ProcessIdEcho,
    AutomaticStatusBack,
    TransmitStatus,
    Unrecognised,
}

public sealed class EpsonTmT20ivNetworkPrinterSession : IPrinterSession
{
    private const int WriteChunkSize = 64;
    private const int AsbBlockLength = 4;
    private const byte ProcessIdEchoHeader = 0x37;
    private const byte ProcessIdEchoIdentifier = 0x22;
    private const byte DataLinkEscape = 0x10;
    private const byte EndOfTransmission = 0x04;
    private const byte FixedBitMask = 0x93;
    private const byte AutomaticStatusBackFixedBits = 0x10;
    private const byte TransmitStatusFixedBits = 0x12;
    private const byte CoverOpenBit = 0x04;
    private const byte ErrorOccurredBit = 0x40;
    private const byte OfflineBit = 0x08;
    private const byte PaperEndMask = 0x60;
    private const byte PaperNearEndMask = 0x0C;

    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly PrinterSessionTimeouts timeouts;
    private readonly TimeProvider timeProvider;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<PrinterStatusSnapshot> statusChannel = Channel.CreateUnbounded<PrinterStatusSnapshot>();
    private readonly ConcurrentQueue<TaskCompletionSource<byte>> pendingQueries = new();
    private readonly List<byte> inbound = [];
    private readonly Lock guard = new();
    private readonly SemaphoreSlim writeGate = new(1, 1);

    private readonly byte[] initialise = [0x1B, 0x40];
    private readonly byte[] selectCodePage = [0x1B, 0x74, 0x13];
    private readonly byte[] enableAutomaticStatusBack = [0x1D, 0x61, 0x0F];

    private PendingProcessIdEcho? pendingEcho;
    private PrinterStatusSnapshot lastKnownStatus;
    private int unrecognisedInboundByteCount;
    private bool remoteClosed;
    private Task? readLoop;
    private Task? heartbeatLoop;

    public EpsonTmT20ivNetworkPrinterSession(TcpClient client, PrinterSessionTimeouts timeouts, TimeProvider timeProvider)
    {
        this.client = client;
        this.timeouts = timeouts;
        this.timeProvider = timeProvider;
        stream = client.GetStream();
        lastKnownStatus = new PrinterStatusSnapshot(true, false, false, false, false, "Connected.", timeProvider.GetUtcNow());
    }

    public int UnrecognisedInboundByteCount
    {
        get
        {
            lock (guard)
            {
                return unrecognisedInboundByteCount;
            }
        }
    }

    public IAsyncEnumerable<PrinterStatusSnapshot> StatusStream
    {
        get { return statusChannel.Reader.ReadAllAsync(); }
    }

    public async Task StartAsync()
    {
        await WriteAllAsync([.. initialise, .. selectCodePage, .. enableAutomaticStatusBack], lifetime.Token);
        readLoop = Task.Run(() => ReadLoopAsync(lifetime.Token), CancellationToken.None);
        heartbeatLoop = Task.Run(() => HeartbeatLoopAsync(lifetime.Token), CancellationToken.None);
    }

    public async Task<PrinterStatusSnapshot> QueryStatusAsync(CancellationToken cancellationToken)
    {
        byte? printerStatus = await QueryAsync(1, cancellationToken);
        byte? offlineStatus = await QueryAsync(2, cancellationToken);
        byte? paperStatus = await QueryAsync(4, cancellationToken);

        PrinterStatusSnapshot snapshot = new(
            printerStatus is null || (printerStatus.Value & OfflineBit) == 0,
            paperStatus is not null && (paperStatus.Value & PaperEndMask) == PaperEndMask,
            paperStatus is not null && (paperStatus.Value & PaperNearEndMask) != 0,
            offlineStatus is not null && (offlineStatus.Value & CoverOpenBit) != 0,
            offlineStatus is not null && (offlineStatus.Value & ErrorOccurredBit) != 0,
            $"DLE EOT n=1,2,4 answered {Describe(printerStatus)},{Describe(offlineStatus)},{Describe(paperStatus)}.",
            timeProvider.GetUtcNow());

        Publish(snapshot);
        return snapshot;
    }

    public async Task<PrintDispatchResult> SendJobAsync(PrintPayload payload, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> echoCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (guard)
        {
            pendingEcho = new PendingProcessIdEcho(payload.PrinterJobId, echoCompletion);
        }

        int bytesWritten = 0;
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            if (RemoteHasClosed())
            {
                return Dropped(0, "The connection was closed before the first byte was written.");
            }

            ReadOnlyMemory<byte> bytes = payload.Bytes;
            while (bytesWritten < bytes.Length)
            {
                int take = Math.Min(WriteChunkSize, bytes.Length - bytesWritten);
                await stream.WriteAsync(bytes.Slice(bytesWritten, take), cancellationToken);
                bytesWritten += take;

                if (RemoteHasClosed())
                {
                    return Dropped(bytesWritten, "The connection dropped while the payload was being written.");
                }
            }

            await stream.WriteAsync(ProcessIdRequest(payload.PrinterJobId), cancellationToken);
        }
        catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException)
        {
            return Dropped(bytesWritten, error.Message);
        }
        finally
        {
            writeGate.Release();
        }

        Task completed = await Task.WhenAny(echoCompletion.Task, Task.Delay(timeouts.JobTimeout, cancellationToken));
        lock (guard)
        {
            pendingEcho = null;
        }

        if (completed != echoCompletion.Task)
        {
            return new PrintDispatchResult(
                PrintOutcome.Timeout,
                bytesWritten,
                lastKnownStatus,
                $"No process id echo for {payload.PrinterJobId.ToString(CultureInfo.InvariantCulture)} arrived within {timeouts.JobTimeout}.");
        }

        return new PrintDispatchResult(
            PrintOutcome.Confirmed,
            bytesWritten,
            lastKnownStatus,
            $"Process id {payload.PrinterJobId.ToString(CultureInfo.InvariantCulture)} echoed on the sending connection.");
    }

    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync();
        statusChannel.Writer.TryComplete();
        client.Dispose();

        if (readLoop is not null)
        {
            await Task.WhenAny(readLoop, Task.Delay(500));
        }

        if (heartbeatLoop is not null)
        {
            await Task.WhenAny(heartbeatLoop, Task.Delay(500));
        }

        lifetime.Dispose();
        writeGate.Dispose();
    }

    private PrintDispatchResult Dropped(int bytesWritten, string detail)
    {
        lock (guard)
        {
            pendingEcho = null;
        }

        return new PrintDispatchResult(PrintOutcome.SocketDropped, bytesWritten, lastKnownStatus, detail);
    }

    private string Describe(byte? value)
    {
        return value is null ? "nothing" : $"0x{value.Value:X2}";
    }

    private byte[] ProcessIdRequest(int processId)
    {
        byte[] digits = Encoding.ASCII.GetBytes(processId.ToString("D4", CultureInfo.InvariantCulture));
        return [0x1D, 0x28, 0x48, 0x06, 0x00, 0x30, 0x30, .. digits];
    }

    private bool RemoteHasClosed()
    {
        lock (guard)
        {
            return remoteClosed;
        }
    }

    private void MarkRemoteClosed()
    {
        lock (guard)
        {
            remoteClosed = true;
        }
    }

    private async Task<byte?> QueryAsync(int n, CancellationToken cancellationToken)
    {
        TaskCompletionSource<byte> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingQueries.Enqueue(completion);

        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await stream.WriteAsync(new byte[] { DataLinkEscape, EndOfTransmission, (byte)n }, cancellationToken);
        }
        catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException)
        {
            completion.TrySetResult(0x00);
            return null;
        }
        finally
        {
            writeGate.Release();
        }

        Task finished = await Task.WhenAny(completion.Task, Task.Delay(timeouts.StatusQueryTimeout, cancellationToken));
        if (finished != completion.Task)
        {
            completion.TrySetCanceled();
            return null;
        }

        return await completion.Task;
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024];
        while (!cancellationToken.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer, cancellationToken);
            }
            catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
            {
                MarkRemoteClosed();
                return;
            }

            if (read == 0)
            {
                MarkRemoteClosed();
                return;
            }

            lock (guard)
            {
                inbound.AddRange(buffer[..read]);
            }

            DrainInbound();
        }
    }

    private void DrainInbound()
    {
        while (DrainOne())
        {
        }
    }

    private bool DrainOne()
    {
        InboundFrame frame = PeekFrame();

        switch (frame)
        {
            case InboundFrame.Incomplete:
                return false;
            case InboundFrame.ProcessIdEcho:
                int? echoed = TakeProcessIdEcho();
                if (echoed is null)
                {
                    return false;
                }

                CompleteEcho(echoed.Value);
                return true;
            case InboundFrame.AutomaticStatusBack:
                byte[]? asb = TakeAsbBlock();
                if (asb is null)
                {
                    return false;
                }

                Publish(DecodeAsb(asb));
                return true;
            case InboundFrame.TransmitStatus:
                if (!pendingQueries.TryDequeue(out TaskCompletionSource<byte>? query))
                {
                    DiscardOneUnrecognisedByte();
                    return true;
                }

                byte? status = TakeSingleByte();
                if (status is null)
                {
                    return false;
                }

                query.TrySetResult(status.Value);
                return true;
            case InboundFrame.Unrecognised:
                DiscardOneUnrecognisedByte();
                return true;
            default:
                return new Never().OfType<bool>(frame);
        }
    }

    private InboundFrame PeekFrame()
    {
        lock (guard)
        {
            if (inbound.Count == 0)
            {
                return InboundFrame.Incomplete;
            }

            byte head = inbound[0];

            if (head == ProcessIdEchoHeader)
            {
                if (inbound.Count < 2)
                {
                    return InboundFrame.Incomplete;
                }

                return inbound[1] == ProcessIdEchoIdentifier ? InboundFrame.ProcessIdEcho : InboundFrame.Unrecognised;
            }

            if ((head & FixedBitMask) == AutomaticStatusBackFixedBits)
            {
                return InboundFrame.AutomaticStatusBack;
            }

            if ((head & FixedBitMask) == TransmitStatusFixedBits)
            {
                return InboundFrame.TransmitStatus;
            }

            return InboundFrame.Unrecognised;
        }
    }

    private void DiscardOneUnrecognisedByte()
    {
        lock (guard)
        {
            if (inbound.Count == 0)
            {
                return;
            }

            inbound.RemoveAt(0);
            unrecognisedInboundByteCount++;
        }
    }

    private byte? TakeSingleByte()
    {
        lock (guard)
        {
            if (inbound.Count == 0)
            {
                return null;
            }

            byte value = inbound[0];
            inbound.RemoveAt(0);
            return value;
        }
    }

    private int? TakeProcessIdEcho()
    {
        lock (guard)
        {
            if (inbound.Count < 2 || inbound[0] != ProcessIdEchoHeader || inbound[1] != ProcessIdEchoIdentifier)
            {
                return null;
            }

            int terminator = inbound.IndexOf(0x00);
            if (terminator < 2)
            {
                return null;
            }

            string digits = Encoding.ASCII.GetString([.. inbound.GetRange(2, terminator - 2)]);
            inbound.RemoveRange(0, terminator + 1);
            return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : -1;
        }
    }

    private byte[]? TakeAsbBlock()
    {
        lock (guard)
        {
            if (inbound.Count < AsbBlockLength)
            {
                return null;
            }

            byte[] block = [.. inbound.GetRange(0, AsbBlockLength)];
            inbound.RemoveRange(0, AsbBlockLength);
            return block;
        }
    }

    private void CompleteEcho(int processId)
    {
        lock (guard)
        {
            if (pendingEcho is not null && pendingEcho.ProcessId == processId)
            {
                pendingEcho.Completion.TrySetResult(true);
                pendingEcho = null;
            }
        }
    }

    private PrinterStatusSnapshot DecodeAsb(byte[] block)
    {
        return new PrinterStatusSnapshot(
            (block[0] & OfflineBit) == 0,
            (block[2] & PaperEndMask) == PaperEndMask,
            (block[2] & PaperNearEndMask) != 0,
            (block[1] & CoverOpenBit) != 0,
            (block[1] & ErrorOccurredBit) != 0,
            $"Automatic status back block {Convert.ToHexString(block)}.",
            timeProvider.GetUtcNow());
    }

    private void Publish(PrinterStatusSnapshot snapshot)
    {
        lastKnownStatus = snapshot;
        statusChannel.Writer.TryWrite(snapshot);
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        int unanswered = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(timeouts.HeartbeatInterval, cancellationToken);
                byte? answer = await QueryAsync(4, cancellationToken);
                unanswered = answer is null ? unanswered + 1 : 0;

                if (unanswered == 2)
                {
                    Publish(new PrinterStatusSnapshot(
                        false,
                        lastKnownStatus.IsPaperEnd,
                        lastKnownStatus.IsPaperNearEnd,
                        lastKnownStatus.IsCoverOpen,
                        lastKnownStatus.IsInErrorState,
                        "Two consecutive heartbeats went unanswered.",
                        timeProvider.GetUtcNow()));
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task WriteAllAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await stream.WriteAsync(bytes, cancellationToken);
        }
        finally
        {
            writeGate.Release();
        }
    }
}
