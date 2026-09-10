namespace GastronomyApp.Core.Entities;

public sealed class Station
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required int SortOrder { get; set; }

  public required bool IsActive { get; set; }

  public Guid? DeviceId { get; set; }

  public Guid? EnrolmentInvitationId { get; set; }
}
