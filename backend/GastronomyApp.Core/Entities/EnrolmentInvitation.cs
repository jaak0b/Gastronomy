namespace GastronomyApp.Core.Entities;

public sealed class EnrolmentInvitation
{
    public required Guid Id { get; set; }
    public Guid? StaffMemberId { get; set; }
    public required byte[] QrCodeHash { get; set; }
    public required byte[] QrCodeSalt { get; set; }
    public required byte[] SixDigitHash { get; set; }
    public required byte[] SixDigitSalt { get; set; }
    public required int CodeIterations { get; set; }
    public required string CodeAlgorithm { get; set; }
    public required int FailedSixDigitAttempts { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByDeviceId { get; set; }
}
