using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Ports;

public interface IDeviceTokenStore
{
  public Task<IssuedDeviceToken> IssueAsync(DeviceOwner owner, string language, string userAgentSnapshot, CancellationToken cancellationToken);

  public Task<DeviceVerificationResult> VerifyAsync(string tokenLookupId, string secret, CancellationToken cancellationToken);

  public Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken);
}
