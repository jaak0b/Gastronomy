namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record RedeemBody(string? Code, string? Name, string? PreviousDeviceToken = null);
