using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Ports;

public interface IEnrolmentInvitationStore
{
  public Task<IssuedEnrolmentInvitation> CreateAsync(IDeviceOwner? owner, CancellationToken cancellationToken);

  public Task<EnrolmentRedemptionResult> RedeemAsync(string code, string? name, string userAgent, string acceptLanguageHeader, CancellationToken cancellationToken);

  public Task<EnrolmentInvitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken);

  public Task ConsumeAsync(Guid invitationId, DateTime consumedAtUtc, CancellationToken cancellationToken);
}
