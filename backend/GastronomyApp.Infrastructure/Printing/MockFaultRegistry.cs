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
    UnknownOutcome,
}

public enum MockFaultMode
{
    Once,
    Sticky,
}

public sealed record ArmedMockFault(MockFault Fault, MockFaultMode Mode);

public interface IMockFaultRegistry
{
    public MockFault GetArmedFault(Guid stationId);

    public void Arm(Guid stationId, MockFault fault, MockFaultMode mode);

    public void ClearIfOnce(Guid stationId);
}

public sealed class InMemoryMockFaultRegistry : IMockFaultRegistry
{
    private readonly ConcurrentDictionary<Guid, ArmedMockFault> armed = new();

    public MockFault GetArmedFault(Guid stationId)
    {
        return armed.TryGetValue(stationId, out ArmedMockFault? entry) ? entry.Fault : MockFault.None;
    }

    public void Arm(Guid stationId, MockFault fault, MockFaultMode mode)
    {
        armed[stationId] = new ArmedMockFault(fault, mode);
    }

    public void ClearIfOnce(Guid stationId)
    {
        if (armed.TryGetValue(stationId, out ArmedMockFault? entry) && entry.Mode == MockFaultMode.Once)
        {
            armed[stationId] = new ArmedMockFault(MockFault.None, MockFaultMode.Sticky);
        }
    }
}
