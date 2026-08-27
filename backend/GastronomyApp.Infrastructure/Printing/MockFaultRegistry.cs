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
    public MockFault GetArmedFault(Guid productionLocationId);

    public void Arm(Guid productionLocationId, MockFault fault, MockFaultMode mode);

    public void ClearIfOnce(Guid productionLocationId);
}

public sealed class InMemoryMockFaultRegistry : IMockFaultRegistry
{
    private readonly ConcurrentDictionary<Guid, ArmedMockFault> armed = new();

    public MockFault GetArmedFault(Guid productionLocationId)
    {
        return armed.TryGetValue(productionLocationId, out ArmedMockFault? entry) ? entry.Fault : MockFault.None;
    }

    public void Arm(Guid productionLocationId, MockFault fault, MockFaultMode mode)
    {
        armed[productionLocationId] = new ArmedMockFault(fault, mode);
    }

    public void ClearIfOnce(Guid productionLocationId)
    {
        if (armed.TryGetValue(productionLocationId, out ArmedMockFault? entry) && entry.Mode == MockFaultMode.Once)
        {
            armed[productionLocationId] = new ArmedMockFault(MockFault.None, MockFaultMode.Sticky);
        }
    }
}
