using System.Security.Cryptography;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Ports;
using GastronomyApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class DeviceTokenStore : IDeviceTokenStore
{
  private const int SecretLengthBytes = 32;
  private readonly IClock _clock;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly DeviceOwnerStore _ownerStore;
  private readonly Pbkdf2SecretHasher _secretHasher;

  public DeviceTokenStore(GastronomyAppDbContext dbContext,
                          DeviceOwnerStore ownerStore,
                          Pbkdf2SecretHasher secretHasher,
                          IClock clock)
  {
    _dbContext = dbContext;
    _ownerStore = ownerStore;
    _secretHasher = secretHasher;
    _clock = clock;
  }

  public async Task<IssuedDeviceToken> IssueAsync(DeviceOwner owner,
                                                  string language,
                                                  string userAgentSnapshot,
                                                  CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);

    var ownerRecord = await _ownerStore.FindAsync(owner, cancellationToken);

    if (ownerRecord is null)
    {
      throw new InvalidOperationException($"A device cannot be issued to the {owner.Kind} {owner.Id}, because no such row exists.");
    }

    if (ownerRecord.DeviceId is not null)
    {
      await RemoveDeviceAsync(ownerRecord.DeviceId.Value, cancellationToken);
    }

    var tokenLookupId = Guid.NewGuid().ToString("N");
    var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretLengthBytes));
    var hashedSecret = _secretHasher.Hash(secret);
    var now = _clock.UtcNow;

    Device device = new()
                    {
                      Id = Guid.NewGuid(),
                      Language = language,
                      TokenHash = hashedSecret.Hash,
                      TokenSalt = hashedSecret.Salt,
                      TokenIterations = hashedSecret.Iterations,
                      TokenAlgorithm = hashedSecret.Algorithm,
                      TokenLookupId = tokenLookupId,
                      CreatedAtUtc = now,
                      LastSeenAtUtc = now
                    };

    _dbContext.Devices.Add(device);
    await _ownerStore.PointDeviceAsync(owner, device.Id, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(device, $"{tokenLookupId}.{secret}");
  }

  public async Task<DeviceVerificationResult> VerifyAsync(string tokenLookupId,
                                                          string secret,
                                                          CancellationToken cancellationToken)
  {
    var device = await _dbContext.Devices
                                 .FirstOrDefaultAsync(candidate => candidate.TokenLookupId == tokenLookupId, cancellationToken);

    if (device is null)
    {
      return new(false, null, null);
    }

    var secretMatches = _secretHasher.Verify(secret,
                                             device.TokenHash,
                                             device.TokenSalt,
                                             device.TokenIterations,
                                             device.TokenAlgorithm);

    if (!secretMatches)
    {
      return new(false, null, null);
    }

    var owner = await _ownerStore.FindByDeviceAsync(device.Id, cancellationToken);

    if (owner is null)
    {
      return new(false, null, null);
    }

    device.LastSeenAtUtc = _clock.UtcNow;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(true, device, owner);
  }

  public async Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var removed = await RemoveDeviceAsync(deviceId, cancellationToken);

    if (removed)
    {
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  private async Task<bool> RemoveDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var device = await _dbContext.Devices
                                 .FirstOrDefaultAsync(candidate => candidate.Id == deviceId, cancellationToken);

    if (device is null)
    {
      return false;
    }

    var owner = await _ownerStore.FindByDeviceAsync(deviceId, cancellationToken);

    if (owner is not null)
    {
      await _ownerStore.PointDeviceAsync(owner, null, cancellationToken);
    }

    _dbContext.Devices.Remove(device);

    return true;
  }
}
