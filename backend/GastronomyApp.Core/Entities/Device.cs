namespace GastronomyApp.Core.Entities;

public sealed class Device
{
  public required Guid Id { get; set; }
  public required Guid StaffMemberId { get; set; }
  public required string Language { get; set; }
  public required byte[] TokenHash { get; set; }
  public required byte[] TokenSalt { get; set; }
  public required int TokenIterations { get; set; }
  public required string TokenAlgorithm { get; set; }
  public required string TokenLookupId { get; set; }
  public required DateTime CreatedAtUtc { get; set; }
  public required DateTime LastSeenAtUtc { get; set; }
}
