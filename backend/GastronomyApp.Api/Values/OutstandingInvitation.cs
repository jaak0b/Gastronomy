namespace GastronomyApp.Api.Values;

public sealed record OutstandingInvitation(Guid InvitationId, string QRCodeValue, string QRUrl, DateTime ExpiresAtUtc);
