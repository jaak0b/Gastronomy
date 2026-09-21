using System.Collections.ObjectModel;
using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Core.Entities;

public sealed class StaffMember : IDeviceOwner
{
  public required Guid Id { get; set; }

  public required string Name { get; set; }

  public required bool IsActive { get; set; }

  public Guid? DeviceId { get; set; }

  public Guid? EnrolmentInvitationId { get; set; }

  public required DateTime CreatedAtUtc { get; set; }

  public Device? Device { get; set; }

  public EnrolmentInvitation? EnrolmentInvitation { get; set; }

  public Collection<Order> Orders { get; } = [];

  DeviceOwnerKind IDeviceOwner.Kind => DeviceOwnerKind.StaffMember;

  public bool HasOutstandingInvitation()
  {
    return EnrolmentInvitation is not null;
  }
}
