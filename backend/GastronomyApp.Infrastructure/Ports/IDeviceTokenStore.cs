using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.Ports;

public sealed record IssuedDeviceToken(Device Device, string PlaintextToken);

public sealed record DeviceVerificationResult(bool IsValid, Device? Device);

public interface IDeviceTokenStore
{
  public Task<IssuedDeviceToken> IssueAsync(Guid staffMemberId,
                                            string language,
                                            string userAgentSnapshot,
                                            CancellationToken cancellationToken);

  public Task<DeviceVerificationResult> VerifyAsync(string tokenLookupId,
                                                    string secret,
                                                    CancellationToken cancellationToken);

  public Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken);
}
