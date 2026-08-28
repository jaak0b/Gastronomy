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
  private readonly Pbkdf2SecretHasher _secretHasher;

  public DeviceTokenStore(GastronomyAppDbContext dbContext, Pbkdf2SecretHasher secretHasher, IClock clock)
  {
    _dbContext = dbContext;
    _secretHasher = secretHasher;
    _clock = clock;
  }

  public async Task<IssuedDeviceToken> IssueAsync(Guid staffMemberId,
                                                  string language,
                                                  string userAgentSnapshot,
                                                  CancellationToken cancellationToken)
  {
    var tokenLookupId = Guid.NewGuid().ToString("N");
    var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretLengthBytes));
    var hashedSecret = _secretHasher.Hash(secret);
    var now = _clock.UtcNow;

    Device device = new()
                    {
                      Id = Guid.NewGuid(),
                      StaffMemberId = staffMemberId,
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
      return new(false, null);
    }

    var secretMatches = _secretHasher.Verify(secret,
                                             device.TokenHash,
                                             device.TokenSalt,
                                             device.TokenIterations,
                                             device.TokenAlgorithm);

    if (!secretMatches)
    {
      return new(false, null);
    }

    device.LastSeenAtUtc = _clock.UtcNow;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(true, device);
  }

  public async Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var device = await _dbContext.Devices
                                 .FirstOrDefaultAsync(candidate => candidate.Id == deviceId, cancellationToken);

    if (device is null)
    {
      return;
    }

    _dbContext.Devices.Remove(device);
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
