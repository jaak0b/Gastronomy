using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IEnrolmentInvitationStore
{
  public Task<EnrolmentInvitationCreated> CreateAsync(DeviceOwner? owner, CancellationToken cancellationToken);

  public Task<EnrolmentRedemptionResult> RedeemAsync(EnrolmentRedemptionRequest request, CancellationToken cancellationToken);

  public Task<EnrolmentInvitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken);

  public Task ConsumeAsync(Guid invitationId, DateTime consumedAtUtc, CancellationToken cancellationToken);
}
