using System.Collections.Concurrent;

namespace GastronomyApp.Infrastructure.Printing;

public enum MockFault
{
  None,
  PaperEnd,
  CoverOpen,
  ConnectTimeout,
  DropSocketEarly,
  DropSocketMidJob,
  UnknownOutcome
}

public enum MockFaultMode
{
  Once,
  Sticky
}

public sealed record ArmedMockFault(MockFault Fault, MockFaultMode Mode);

public interface IMockFaultRegistry
{
  public MockFault GetArmedFault(Guid printerId);

  public ArmedMockFault Armed(Guid printerId);

  public void Arm(Guid printerId, MockFault fault, MockFaultMode mode);

  public void ClearIfOnce(Guid printerId);
}

public sealed class InMemoryMockFaultRegistry : IMockFaultRegistry
{
  private readonly ConcurrentDictionary<Guid, ArmedMockFault> _armed = new();

  public MockFault GetArmedFault(Guid printerId)
  {
    return Armed(printerId).Fault;
  }

  public ArmedMockFault Armed(Guid printerId)
  {
    return _armed.TryGetValue(printerId, out var entry)
             ? entry
             : new(MockFault.None, MockFaultMode.Once);
  }

  public void Arm(Guid printerId, MockFault fault, MockFaultMode mode)
  {
    _armed[printerId] = new(fault, mode);
  }

  public void ClearIfOnce(Guid printerId)
  {
    if (_armed.TryGetValue(printerId, out var entry) && entry.Mode == MockFaultMode.Once)
    {
      _armed[printerId] = new(MockFault.None, MockFaultMode.Sticky);
    }
  }
}
