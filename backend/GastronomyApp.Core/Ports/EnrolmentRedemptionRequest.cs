namespace GastronomyApp.Core.Ports;

public sealed record EnrolmentRedemptionRequest(string Code, string? Name, string UserAgent, string AcceptLanguageHeader);
