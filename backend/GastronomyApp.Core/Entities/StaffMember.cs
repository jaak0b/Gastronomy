using System.Collections.ObjectModel;

namespace GastronomyApp.Core.Entities;

public sealed class StaffMember
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

  public bool HasOutstandingInvitation()
  {
    return EnrolmentInvitation is not null;
  }
}
