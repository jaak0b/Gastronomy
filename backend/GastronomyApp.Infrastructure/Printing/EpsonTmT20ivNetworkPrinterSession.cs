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
  Unrecognised
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

  private readonly TcpClient _client;
  private readonly byte[] _enableAutomaticStatusBack = [0x1D, 0x61, 0x0F];
  private readonly Lock _guard = new();
  private readonly List<byte> _inbound = [];

  private readonly byte[] _initialise = [0x1B, 0x40];
  private readonly CancellationTokenSource _lifetime = new();
  private readonly ConcurrentQueue<TaskCompletionSource<byte>> _pendingQueries = new();
  private readonly byte[] _selectCodePage = [0x1B, 0x74, 0x13];
  private readonly Channel<PrinterStatusSnapshot> statusChannel = Channel.CreateUnbounded<PrinterStatusSnapshot>();
  private readonly NetworkStream _stream;
  private readonly PrinterSessionTimeouts _timeouts;
  private readonly TimeProvider _timeProvider;
  private readonly SemaphoreSlim _writeGate = new(1, 1);
  private Task? _heartbeatLoop;
  private PrinterStatusSnapshot _lastKnownStatus;

  private PendingProcessIdEcho? _pendingEcho;
  private Task? _readLoop;
  private bool _remoteClosed;
  private int _unrecognisedInboundByteCount;

  public EpsonTmT20ivNetworkPrinterSession(TcpClient client, PrinterSessionTimeouts timeouts, TimeProvider timeProvider)
  {
    _client = client;
    _timeouts = timeouts;
    _timeProvider = timeProvider;
    _stream = client.GetStream();
    _lastKnownStatus = new(true, false, false, false, false, "Connected.", timeProvider.GetUtcNow());
  }

  public int UnrecognisedInboundByteCount
  {
    get
    {
      lock (_guard)
      {
        return _unrecognisedInboundByteCount;
      }
    }
  }

  public IAsyncEnumerable<PrinterStatusSnapshot> StatusStream => statusChannel.Reader.ReadAllAsync();

  public async Task<PrinterStatusSnapshot> QueryStatusAsync(CancellationToken cancellationToken)
  {
    var printerStatus = await QueryAsync(1, cancellationToken);
    var offlineStatus = await QueryAsync(2, cancellationToken);
    var paperStatus = await QueryAsync(4, cancellationToken);

    PrinterStatusSnapshot snapshot = new(printerStatus is null || (printerStatus.Value & OfflineBit) == 0,
                                         paperStatus is not null && (paperStatus.Value & PaperEndMask) == PaperEndMask,
                                         paperStatus is not null && (paperStatus.Value & PaperNearEndMask) != 0,
                                         offlineStatus is not null && (offlineStatus.Value & CoverOpenBit) != 0,
                                         offlineStatus is not null && (offlineStatus.Value & ErrorOccurredBit) != 0,
                                         $"DLE EOT n=1,2,4 answered {Describe(printerStatus)},{Describe(offlineStatus)},{Describe(paperStatus)}.",
                                         _timeProvider.GetUtcNow());

    Publish(snapshot);
    return snapshot;
  }

  public async Task<PrintDispatchResult> SendJobAsync(PrintPayload payload, CancellationToken cancellationToken)
  {
    TaskCompletionSource<bool> echoCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    lock (_guard)
    {
      _pendingEcho = new(payload.PrinterJobId, echoCompletion);
    }

    var bytesWritten = 0;
    await _writeGate.WaitAsync(cancellationToken);
    try
    {
      if (RemoteHasClosed())
      {
        return Dropped(0, "The connection was closed before the first byte was written.");
      }

      ReadOnlyMemory<byte> bytes = payload.Bytes;
      while (bytesWritten < bytes.Length)
      {
        var take = Math.Min(WriteChunkSize, bytes.Length - bytesWritten);
        await _stream.WriteAsync(bytes.Slice(bytesWritten, take), cancellationToken);
        bytesWritten += take;

        if (RemoteHasClosed())
        {
          return Dropped(bytesWritten, "The connection dropped while the payload was being written.");
        }
      }

      await _stream.WriteAsync(ProcessIdRequest(payload.PrinterJobId), cancellationToken);
    }
    catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException)
    {
      return Dropped(bytesWritten, error.Message);
    } finally
    {
      _writeGate.Release();
    }

    var completed = await Task.WhenAny(echoCompletion.Task, Task.Delay(_timeouts.JobTimeout, cancellationToken));
    lock (_guard)
    {
      _pendingEcho = null;
    }

    if (completed != echoCompletion.Task)
    {
      return new(PrintOutcome.Timeout,
                 bytesWritten,
                 _lastKnownStatus,
                 $"No process id echo for {payload.PrinterJobId.ToString(CultureInfo.InvariantCulture)} arrived within {_timeouts.JobTimeout}.");
    }

    return new(PrintOutcome.Confirmed,
               bytesWritten,
               _lastKnownStatus,
               $"Process id {payload.PrinterJobId.ToString(CultureInfo.InvariantCulture)} echoed on the sending connection.");
  }

  public async ValueTask DisposeAsync()
  {
    await _lifetime.CancelAsync();
    statusChannel.Writer.TryComplete();
    _client.Dispose();

    if (_readLoop is not null)
    {
      await Task.WhenAny(_readLoop, Task.Delay(500));
    }

    if (_heartbeatLoop is not null)
    {
      await Task.WhenAny(_heartbeatLoop, Task.Delay(500));
    }

    _lifetime.Dispose();
    _writeGate.Dispose();
  }

  public async Task StartAsync()
  {
    await WriteAllAsync([.. _initialise, .. _selectCodePage, .. _enableAutomaticStatusBack], _lifetime.Token);
    _readLoop = Task.Run(() => ReadLoopAsync(_lifetime.Token), CancellationToken.None);
    _heartbeatLoop = Task.Run(() => HeartbeatLoopAsync(_lifetime.Token), CancellationToken.None);
  }

  private PrintDispatchResult Dropped(int bytesWritten, string detail)
  {
    lock (_guard)
    {
      _pendingEcho = null;
    }

    return new(PrintOutcome.SocketDropped, bytesWritten, _lastKnownStatus, detail);
  }

  private string Describe(byte? value)
  {
    return value is null ? "nothing" : $"0x{value.Value:X2}";
  }

  private byte[] ProcessIdRequest(int processId)
  {
    var digits = Encoding.ASCII.GetBytes(processId.ToString("D4", CultureInfo.InvariantCulture));
    return [0x1D, 0x28, 0x48, 0x06, 0x00, 0x30, 0x30, .. digits];
  }

  private bool RemoteHasClosed()
  {
    lock (_guard)
    {
      return _remoteClosed;
    }
  }

  private void MarkRemoteClosed()
  {
    lock (_guard)
    {
      _remoteClosed = true;
    }
  }

  private async Task<byte?> QueryAsync(int n, CancellationToken cancellationToken)
  {
    TaskCompletionSource<byte> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    _pendingQueries.Enqueue(completion);

    await _writeGate.WaitAsync(cancellationToken);
    try
    {
      await _stream.WriteAsync(new[] { DataLinkEscape, EndOfTransmission, (byte)n }, cancellationToken);
    }
    catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException)
    {
      completion.TrySetResult(0x00);
      return null;
    } finally
    {
      _writeGate.Release();
    }

    var finished = await Task.WhenAny(completion.Task, Task.Delay(_timeouts.StatusQueryTimeout, cancellationToken));
    if (finished != completion.Task)
    {
      completion.TrySetCanceled();
      return null;
    }

    return await completion.Task;
  }

  private async Task ReadLoopAsync(CancellationToken cancellationToken)
  {
    var buffer = new byte[1024];
    while (!cancellationToken.IsCancellationRequested)
    {
      int read;
      try
      {
        read = await _stream.ReadAsync(buffer, cancellationToken);
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

      lock (_guard)
      {
        _inbound.AddRange(buffer[..read]);
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
    var frame = PeekFrame();

    switch (frame)
    {
      case InboundFrame.Incomplete:
        return false;
      case InboundFrame.ProcessIdEcho:
        var echoed = TakeProcessIdEcho();
        if (echoed is null)
        {
          return false;
        }

        CompleteEcho(echoed.Value);
        return true;
      case InboundFrame.AutomaticStatusBack:
        var asb = TakeAsbBlock();
        if (asb is null)
        {
          return false;
        }

        Publish(DecodeAsb(asb));
        return true;
      case InboundFrame.TransmitStatus:
        if (!_pendingQueries.TryDequeue(out TaskCompletionSource<byte>? query))
        {
          DiscardOneUnrecognisedByte();
          return true;
        }

        var status = TakeSingleByte();
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
    lock (_guard)
    {
      if (_inbound.Count == 0)
      {
        return InboundFrame.Incomplete;
      }

      var head = _inbound[0];

      if (head == ProcessIdEchoHeader)
      {
        if (_inbound.Count < 2)
        {
          return InboundFrame.Incomplete;
        }

        return _inbound[1] == ProcessIdEchoIdentifier ? InboundFrame.ProcessIdEcho : InboundFrame.Unrecognised;
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
    lock (_guard)
    {
      if (_inbound.Count == 0)
      {
        return;
      }

      _inbound.RemoveAt(0);
      _unrecognisedInboundByteCount++;
    }
  }

  private byte? TakeSingleByte()
  {
    lock (_guard)
    {
      if (_inbound.Count == 0)
      {
        return null;
      }

      var value = _inbound[0];
      _inbound.RemoveAt(0);
      return value;
    }
  }

  private int? TakeProcessIdEcho()
  {
    lock (_guard)
    {
      if (_inbound.Count < 2 || _inbound[0] != ProcessIdEchoHeader || _inbound[1] != ProcessIdEchoIdentifier)
      {
        return null;
      }

      var terminator = _inbound.IndexOf(0x00);
      if (terminator < 2)
      {
        return null;
      }

      var digits = Encoding.ASCII.GetString([.. _inbound.GetRange(2, terminator - 2)]);
      _inbound.RemoveRange(0, terminator + 1);
      return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
    }
  }

  private byte[]? TakeAsbBlock()
  {
    lock (_guard)
    {
      if (_inbound.Count < AsbBlockLength)
      {
        return null;
      }

      byte[] block = [.. _inbound.GetRange(0, AsbBlockLength)];
      _inbound.RemoveRange(0, AsbBlockLength);
      return block;
    }
  }

  private void CompleteEcho(int processId)
  {
    lock (_guard)
    {
      if (_pendingEcho is not null && _pendingEcho.ProcessId == processId)
      {
        _pendingEcho.Completion.TrySetResult(true);
        _pendingEcho = null;
      }
    }
  }

  private PrinterStatusSnapshot DecodeAsb(byte[] block)
  {
    return new((block[0] & OfflineBit) == 0,
               (block[2] & PaperEndMask) == PaperEndMask,
               (block[2] & PaperNearEndMask) != 0,
               (block[1] & CoverOpenBit) != 0,
               (block[1] & ErrorOccurredBit) != 0,
               $"Automatic status back block {Convert.ToHexString(block)}.",
               _timeProvider.GetUtcNow());
  }

  private void Publish(PrinterStatusSnapshot snapshot)
  {
    _lastKnownStatus = snapshot;
    statusChannel.Writer.TryWrite(snapshot);
  }

  private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
  {
    var unanswered = 0;
    while (!cancellationToken.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(_timeouts.HeartbeatInterval, cancellationToken);
        var answer = await QueryAsync(4, cancellationToken);
        unanswered = answer is null ? unanswered + 1 : 0;

        if (unanswered == 2)
        {
          Publish(new(false,
                      _lastKnownStatus.IsPaperEnd,
                      _lastKnownStatus.IsPaperNearEnd,
                      _lastKnownStatus.IsCoverOpen,
                      _lastKnownStatus.IsInErrorState,
                      "Two consecutive heartbeats went unanswered.",
                      _timeProvider.GetUtcNow()));
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
    await _writeGate.WaitAsync(cancellationToken);
    try
    {
      await _stream.WriteAsync(bytes, cancellationToken);
    } finally
    {
      _writeGate.Release();
    }
  }
}
