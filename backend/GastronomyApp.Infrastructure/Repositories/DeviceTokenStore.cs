using System.Security.Cryptography;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class DeviceTokenStore : IDeviceTokenStore
{
  private const int SecretLengthBytes = 32;
  private readonly TimeProvider _timeProvider;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly IDeviceOwnerStore _ownerStore;
  private readonly Pbkdf2SecretHasher _secretHasher;

  public DeviceTokenStore(GastronomyAppDbContext dbContext, IDeviceOwnerStore ownerStore, Pbkdf2SecretHasher secretHasher, TimeProvider timeProvider)
  {
    _dbContext = dbContext;
    _ownerStore = ownerStore;
    _secretHasher = secretHasher;
    _timeProvider = timeProvider;
  }

  public async Task<IssuedDeviceToken> IssueAsync(IDeviceOwner owner, string language, string userAgentSnapshot, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(owner);

    await RetireCurrentDeviceAsync(owner, cancellationToken);

    var tokenLookupId = Guid.NewGuid().ToString("N");
    var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretLengthBytes));
    var hashedSecret = _secretHasher.Hash(secret);
    var now = _timeProvider.GetUtcNow().UtcDateTime;

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
    owner.DeviceId = device.Id;
    owner.Device = device;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(device, $"{tokenLookupId}.{secret}");
  }

  public async Task<IDeviceOwner?> VerifyAsync(string tokenLookupId, string secret, CancellationToken cancellationToken)
  {
    var device = await _dbContext.Devices.FirstOrDefaultAsync(candidate => candidate.TokenLookupId == tokenLookupId, cancellationToken);

    if (device is null)
      return null;

    var secretMatches = _secretHasher.Verify(secret, device.TokenHash, device.TokenSalt, device.TokenIterations, device.TokenAlgorithm);

    if (!secretMatches)
      return null;

    var owner = await _ownerStore.FindByDeviceAsync(device.Id, cancellationToken);

    if (owner is null)
      return null;

    device.LastSeenAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return owner;
  }

  public async Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var device = await _dbContext.Devices.FirstOrDefaultAsync(candidate => candidate.Id == deviceId, cancellationToken);

    if (device is null)
      return;

    var owner = await _ownerStore.FindByDeviceAsync(deviceId, cancellationToken);

    if (owner is not null)
    {
      owner.DeviceId = null;
      owner.Device = null;
    }

    _dbContext.Devices.Remove(device);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }

  private async Task RetireCurrentDeviceAsync(IDeviceOwner owner, CancellationToken cancellationToken)
  {
    if (owner.DeviceId is null)
      return;

    var previousDeviceId = owner.DeviceId.Value;
    var previousDevice = await _dbContext.Devices.FirstOrDefaultAsync(candidate => candidate.Id == previousDeviceId, cancellationToken);

    owner.DeviceId = null;
    owner.Device = null;

    if (previousDevice is not null)
      _dbContext.Devices.Remove(previousDevice);
  }
}
