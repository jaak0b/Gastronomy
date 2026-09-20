namespace GastronomyApp.Api.Hosting;

public sealed record OutstandingInvitation(Guid InvitationId, string QRCodeValue, string QRUrl, DateTime ExpiresAtUtc);
