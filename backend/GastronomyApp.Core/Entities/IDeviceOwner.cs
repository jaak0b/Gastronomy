namespace GastronomyApp.Core.Entities;

public interface IDeviceOwner
{
  public Guid Id { get; }

  public string Name { get; }

  public bool IsActive { get; }

  public Guid? DeviceId { get; set; }

  public Device? Device { get; set; }

  public Guid? EnrolmentInvitationId { get; set; }

  public EnrolmentInvitation? EnrolmentInvitation { get; set; }
}
