using GastronomyApp.Core.Announcements;

namespace GastronomyApp.Core.Entities;

[Raises]
public sealed class EnrolmentInvitation
{
  public required Guid Id { get; set; }

  public required byte[] QRCodeHash { get; set; }

  public required byte[] QRCodeSalt { get; set; }

  public required int QRCodeIterations { get; set; }

  public required string QRCodeAlgorithm { get; set; }

  public required DateTime CreatedAtUtc { get; set; }

  public required DateTime ExpiresAtUtc { get; set; }

  public DateTime? ConsumedAtUtc { get; set; }

  public Guid? ConsumedByDeviceId { get; set; }

  public Device? ConsumedByDevice { get; set; }
}
