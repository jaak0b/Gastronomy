namespace GastronomyApp.Api.Contracts;

public sealed record ServerPersonView(Guid Id, string Name);

public sealed record EventSessionView(Guid Id, string Name, bool IsPractice);

public sealed record SessionView(
    Guid DeviceId,
    ServerPersonView ServerPerson,
    EventSessionView? EventSession,
    string Language);

public sealed record LanguageChangeRequest(string? Language);
