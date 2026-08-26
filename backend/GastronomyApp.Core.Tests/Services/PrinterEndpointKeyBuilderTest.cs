using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class PrinterEndpointKeyBuilderTest
{
    private PrinterEndpointKeyBuilder _builder = new();

    [SetUp]
    public void SetUp()
    {
        _builder = new PrinterEndpointKeyBuilder();
    }

    [Test]
    public void Build_NetworkTransportWithHostAndPort_RendersHostAndPortAndEmptyAgentSegment()
    {
        string key = _builder.Build(TransportKind.Network, "192.168.1.23", 9100, string.Empty);

        Assert.That(key, Is.EqualTo("Network|192.168.1.23|9100|"));
    }

    [Test]
    public void Build_AgentTransportWithoutHostOrPort_RendersEmptyHostAndPortSegments()
    {
        string key = _builder.Build(TransportKind.Agent, string.Empty, null, "agent-1");

        Assert.That(key, Is.EqualTo("Agent|||agent-1"));
    }

    [Test]
    public void Build_MockTransportWithNothingSet_RendersEmptySegmentsOnly()
    {
        string key = _builder.Build(TransportKind.Mock, string.Empty, null, string.Empty);

        Assert.That(key, Is.EqualTo("Mock|||"));
    }

    [Test]
    public void Build_IdenticalInputs_ProducesEqualKeys()
    {
        string first = _builder.Build(TransportKind.Network, "10.0.0.5", 9100, string.Empty);
        string second = _builder.Build(TransportKind.Network, "10.0.0.5", 9100, string.Empty);

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Build_InputsDifferingOnlyInHost_ProducesDifferentKeys()
    {
        string first = _builder.Build(TransportKind.Network, "10.0.0.5", 9100, string.Empty);
        string second = _builder.Build(TransportKind.Network, "10.0.0.6", 9100, string.Empty);

        Assert.That(first, Is.Not.EqualTo(second));
    }
}
