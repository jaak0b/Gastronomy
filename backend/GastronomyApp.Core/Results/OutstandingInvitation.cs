namespace GastronomyApp.Core.Results;

public sealed record OutstandingInvitation(Guid InvitationId, string QRCodeValue, string QRUrl, DateTime ExpiresAtUtc);
