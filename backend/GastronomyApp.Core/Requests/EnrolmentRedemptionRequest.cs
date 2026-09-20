namespace GastronomyApp.Core.Requests;

public sealed record EnrolmentRedemptionRequest(string Code, string? Name, string UserAgent, string AcceptLanguageHeader);
