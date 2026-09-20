namespace GastronomyApp.Infrastructure.Ports;

public interface IEnrolmentInvitationStore
{
  public Task<EnrolmentInvitationCreated> CreateAsync(DeviceOwner? owner, CancellationToken cancellationToken);

  public Task<EnrolmentRedemptionResult> RedeemAsync(EnrolmentRedemptionRequest request,
                                                     CancellationToken cancellationToken);
}
