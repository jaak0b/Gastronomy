using System.Globalization;
using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class PrinterEndpointKeyBuilder
{
    public string Build(TransportKind transportKind, string host, int? port, string agentIdentifier)
    {
        string portSegment = port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        return string.Join('|', transportKind.ToString(), host, portSegment, agentIdentifier);
    }
}
