namespace GastronomyApp.Api.Tests.Endpoints;

public sealed record RedeemBody(string? Code, string? Name, string UserAgent, string? PreviousDeviceToken = null);
