using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GastronomyApp.Infrastructure.Tests.Printing;

sealed internal record ProcessIdEchoScript(bool Enabled, TimeSpan AfterDelay, int? OverrideProcessId);

sealed internal class FakeEscPosPrinterServer : IAsyncDisposable
{
  private readonly Dictionary<int, byte> _dleEotResponses = [];
  private readonly Lock _guard = new();
  private readonly CancellationTokenSource _lifetime = new();
  private readonly TcpListener _listener;
  private readonly List<byte> _receivedBytes = [];
  private int _acceptedConnectionCount;
  private Task? _acceptLoop;

  private byte[]? _asbOnConnect;
  private byte[]? _burstOnProcessIdRequest;
  private int? _dropAfterBytes;
  private bool _dropImmediately;
  private ProcessIdEchoScript _processIdEcho = new(true, TimeSpan.Zero, null);

  public FakeEscPosPrinterServer(int port)
  {
    _listener = new(IPAddress.Loopback, port);
  }

  public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

  public int AcceptedConnectionCount
  {
    get
    {
      lock (_guard)
      {
        return _acceptedConnectionCount;
      }
    }
  }

  public IReadOnlyList<byte> ReceivedBytes
  {
    get
    {
      lock (_guard)
      {
        return [.. _receivedBytes];
      }
    }
  }

  public async ValueTask DisposeAsync()
  {
    await _lifetime.CancelAsync();
    _listener.Stop();
    if (_acceptLoop is not null)
    {
      await Task.WhenAny(_acceptLoop, Task.Delay(500));
    }

    _lifetime.Dispose();
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _listener.Start();
    _acceptLoop = Task.Run(() => AcceptLoopAsync(_lifetime.Token), CancellationToken.None);
    return Task.CompletedTask;
  }

  public void ScriptAsbOnConnect(byte paperStatusMask, byte errorStatusMask)
  {
    lock (_guard)
    {
      _asbOnConnect = [0x14, errorStatusMask, paperStatusMask, 0x00];
    }
  }

  public void ScriptDleEotResponse(int n, byte statusByte)
  {
    lock (_guard)
    {
      _dleEotResponses[n] = statusByte;
    }
  }

  public void ScriptProcessIdEcho(TimeSpan afterDelay)
  {
    lock (_guard)
    {
      _processIdEcho = new(true, afterDelay, null);
    }
  }

  public void ScriptProcessIdEchoCarrying(int processId)
  {
    lock (_guard)
    {
      _processIdEcho = new(true, TimeSpan.Zero, processId);
    }
  }

  public void ScriptBurstOnProcessIdRequest(byte paperStatusMask, byte errorStatusMask, byte statusByte)
  {
    lock (_guard)
    {
      _burstOnProcessIdRequest = [0x14, errorStatusMask, paperStatusMask, 0x00, statusByte];
    }
  }

  public void ScriptNeverEchoProcessId()
  {
    lock (_guard)
    {
      _processIdEcho = new(false, TimeSpan.Zero, null);
    }
  }

  public void ScriptDropConnectionAfterBytes(int byteCount)
  {
    lock (_guard)
    {
      _dropAfterBytes = byteCount;
    }
  }

  public void ScriptDropConnectionImmediately()
  {
    lock (_guard)
    {
      _dropImmediately = true;
    }
  }

  private async Task AcceptLoopAsync(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      TcpClient client;
      try
      {
        client = await _listener.AcceptTcpClientAsync(cancellationToken);
      }
      catch (Exception error) when (error is OperationCanceledException or SocketException or ObjectDisposedException)
      {
        return;
      }

      lock (_guard)
      {
        _acceptedConnectionCount++;
      }

      _ = Task.Run(() => ServeAsync(client, cancellationToken), CancellationToken.None);
    }
  }

  private async Task ServeAsync(TcpClient client, CancellationToken cancellationToken)
  {
    using var owned = client;
    var stream = owned.GetStream();

    if (ReadDropImmediately())
    {
      owned.Close();
      return;
    }

    var initialAsb = ReadAsbOnConnect();
    if (initialAsb is not null)
    {
      await stream.WriteAsync(initialAsb, cancellationToken);
    }

    var buffer = new byte[4096];
    var totalRead = 0;

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

      var chunk = buffer[..read];
      lock (_guard)
      {
        _receivedBytes.AddRange(chunk);
      }

      totalRead += read;
      var dropAt = ReadDropAfterBytes();
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
    for (var index = 0; index < chunk.Length; index++)
    {
      if (index + 2 < chunk.Length && chunk[index] == 0x10 && chunk[index + 1] == 0x04)
      {
        var response = ReadDleEotResponse(chunk[index + 2]);
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
        var script = ReadProcessIdEcho();
        var burst = ReadBurst();
        if (burst is not null)
        {
          await stream.WriteAsync(burst, cancellationToken);
        }

        if (!script.Enabled)
        {
          continue;
        }

        var processId = script.OverrideProcessId is null
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
    lock (_guard)
    {
      return _burstOnProcessIdRequest;
    }
  }

  private bool ReadDropImmediately()
  {
    lock (_guard)
    {
      return _dropImmediately;
    }
  }

  private byte[]? ReadAsbOnConnect()
  {
    lock (_guard)
    {
      return _asbOnConnect;
    }
  }

  private int? ReadDropAfterBytes()
  {
    lock (_guard)
    {
      return _dropAfterBytes;
    }
  }

  private byte? ReadDleEotResponse(byte n)
  {
    lock (_guard)
    {
      return _dleEotResponses.TryGetValue(n, out var response) ? response : null;
    }
  }

  private ProcessIdEchoScript ReadProcessIdEcho()
  {
    lock (_guard)
    {
      return _processIdEcho;
    }
  }
}
