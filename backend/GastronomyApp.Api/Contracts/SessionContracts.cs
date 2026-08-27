namespace GastronomyApp.Api.Contracts;

public sealed record StaffMemberView(Guid Id, string Name);

public sealed record SessionView(
    Guid DeviceId,
    StaffMemberView StaffMember,
    string Language);

public sealed record LanguageChangeRequest(string? Language);

public sealed record LanguageView(string Language);
