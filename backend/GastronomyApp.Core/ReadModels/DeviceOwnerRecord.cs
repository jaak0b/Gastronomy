using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.ReadModels;

public sealed record DeviceOwnerRecord
{
  public required DeviceOwner Owner { get; init; }

  public required string Name { get; init; }

  public required bool IsActive { get; init; }

  public required Guid? DeviceId { get; init; }

  public required Guid? EnrolmentInvitationId { get; init; }
}
