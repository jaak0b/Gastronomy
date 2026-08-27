namespace GastronomyApp.Api.Contracts;

public sealed record ServerPersonView(Guid Id, string Name);

public sealed record SessionView(
    Guid DeviceId,
    ServerPersonView ServerPerson,
    string Language);

public sealed record LanguageChangeRequest(string? Language);

public sealed record LanguageView(string Language);
