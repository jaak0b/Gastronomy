namespace GastronomyApp.Infrastructure.Ports;

public sealed record EnrolmentInvitationCreated(
  Guid InvitationId,
  string QRCodeValue,
  DateTime ExpiresAtUtc);
