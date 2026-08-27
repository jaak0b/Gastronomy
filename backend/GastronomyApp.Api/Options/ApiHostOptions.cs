namespace GastronomyApp.Api.Options;

public sealed record ApiHostOptions
{
    public required string DataDirectory { get; init; }
    public required int Port { get; init; }
    public required string BindAddress { get; init; }
    public AppLanguage Language { get; init; } = new();
}
