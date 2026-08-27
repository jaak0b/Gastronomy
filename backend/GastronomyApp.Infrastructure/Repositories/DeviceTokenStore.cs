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

    private readonly GastronomyAppDbContext _dbContext;
    private readonly Pbkdf2SecretHasher _secretHasher;
    private readonly IClock _clock;

    public DeviceTokenStore(GastronomyAppDbContext dbContext, Pbkdf2SecretHasher secretHasher, IClock clock)
    {
        _dbContext = dbContext;
        _secretHasher = secretHasher;
        _clock = clock;
    }

    public async Task<IssuedDeviceToken> IssueAsync(
        Guid staffMemberId,
        string language,
        string userAgentSnapshot,
        CancellationToken cancellationToken)
    {
        string tokenLookupId = Guid.NewGuid().ToString("N");
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecretLengthBytes));
        HashedSecret hashedSecret = _secretHasher.Hash(secret);
        DateTime now = _clock.UtcNow;

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
            LastSeenAtUtc = now,
            RevokedAtUtc = null,
            UserAgentSnapshot = userAgentSnapshot,
        };

        _dbContext.Devices.Add(device);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedDeviceToken(device, $"{tokenLookupId}.{secret}");
    }

    public async Task<DeviceVerificationResult> VerifyAsync(
        string tokenLookupId,
        string secret,
        CancellationToken cancellationToken)
    {
        Device? device = await _dbContext.Devices
            .FirstOrDefaultAsync(candidate => candidate.TokenLookupId == tokenLookupId, cancellationToken);

        if (device is null || device.RevokedAtUtc is not null)
        {
            return new DeviceVerificationResult(false, null);
        }

        bool secretMatches = _secretHasher.Verify(
            secret,
            device.TokenHash,
            device.TokenSalt,
            device.TokenIterations,
            device.TokenAlgorithm);

        if (!secretMatches)
        {
            return new DeviceVerificationResult(false, null);
        }

        device.LastSeenAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeviceVerificationResult(true, device);
    }

    public async Task RevokeAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        Device? device = await _dbContext.Devices
            .FirstOrDefaultAsync(candidate => candidate.Id == deviceId, cancellationToken);

        if (device is null)
        {
            return;
        }

        device.RevokedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
