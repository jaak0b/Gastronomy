namespace GastronomyApp.Core.Results;

public sealed record EnrolmentInvitationCreated(Guid InvitationId, string QRCodeValue, DateTime ExpiresAtUtc);
