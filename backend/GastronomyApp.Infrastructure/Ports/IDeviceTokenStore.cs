using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;

namespace GastronomyApp.Infrastructure.Ports;

public sealed record DeviceOwner(DeviceOwnerKind Kind, Guid Id);

public sealed record IssuedDeviceToken(Device Device, string PlaintextToken);

public sealed record DeviceVerificationResult(bool IsValid, Device? Device, DeviceOwner? Owner);

public interface IDeviceTokenStore
{
  public Task<IssuedDeviceToken> IssueAsync(DeviceOwner owner,
                                            string language,
                                            string userAgentSnapshot,
                                            CancellationToken cancellationToken);

  public Task<DeviceVerificationResult> VerifyAsync(string tokenLookupId,
                                                    string secret,
                                                    CancellationToken cancellationToken);

  public Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken);
}
