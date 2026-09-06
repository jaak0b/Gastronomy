namespace GastronomyApp.Core.Entities;

public sealed class EnrolmentInvitation
{
  public required Guid Id { get; set; }

  public required byte[] QrCodeHash { get; set; }

  public required byte[] QrCodeSalt { get; set; }

  public required int QrCodeIterations { get; set; }

  public required string QrCodeAlgorithm { get; set; }

  public required DateTime CreatedAtUtc { get; set; }

  public required DateTime ExpiresAtUtc { get; set; }

  public DateTime? ConsumedAtUtc { get; set; }

  public Guid? ConsumedByDeviceId { get; set; }
}
