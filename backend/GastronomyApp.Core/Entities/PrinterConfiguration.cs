using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class PrinterConfiguration
{
    public required Guid ProductionLocationId { get; set; }
    public required TransportKind TransportKind { get; set; }
    public string? Host { get; set; }
    public required int Port { get; set; }
    public string? AgentIdentifier { get; set; }
    public required int CharactersPerLine { get; set; }
    public required string CodePageName { get; set; }
    public required int ConnectTimeoutSeconds { get; set; }
    public required int JobTimeoutSeconds { get; set; }
    public required int HeartbeatSeconds { get; set; }
    public required bool IsEnabled { get; set; }
}
