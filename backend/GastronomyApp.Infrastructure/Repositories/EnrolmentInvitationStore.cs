using System.Globalization;
using System.Security.Cryptography;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Infrastructure.Ports;
using GastronomyApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class EnrolmentInvitationStore : IEnrolmentInvitationStore
{
    private const int QrCodeLengthBytes = 32;
    private const int MaximumFailedSixDigitAttempts = 10;
    private const int InvitationLifetimeMinutes = 5;
    private const string GermanLanguage = "de";
    private const string EnglishLanguage = "en";

    private readonly GastronomyAppDbContext _dbContext;
    private readonly Pbkdf2SecretHasher _secretHasher;
    private readonly IDeviceTokenStore _deviceTokenStore;
    private readonly IClock _clock;
    private readonly ImmediateTransactionRunner _transactionRunner = new();

    public EnrolmentInvitationStore(
        GastronomyAppDbContext dbContext,
        Pbkdf2SecretHasher secretHasher,
        IDeviceTokenStore deviceTokenStore,
        IClock clock)
    {
        _dbContext = dbContext;
        _secretHasher = secretHasher;
        _deviceTokenStore = deviceTokenStore;
        _clock = clock;
    }

    public Task<EnrolmentInvitationCreated> CreateAsync(Guid? serverPersonId, CancellationToken cancellationToken)
    {
        return _transactionRunner.RunAsync(
            _dbContext,
            async transactionCancellationToken =>
            {
                DateTime now = _clock.UtcNow;

                await ConsumeOutstandingInvitationsAsync(now, transactionCancellationToken);

                string qrCodeValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(QrCodeLengthBytes));
                string sixDigitCode = RandomNumberGenerator.GetInt32(0, 1_000_000)
                    .ToString("D6", CultureInfo.InvariantCulture);

                HashedSecret hashedQrCode = _secretHasher.Hash(qrCodeValue);
                HashedSecret hashedSixDigitCode = _secretHasher.Hash(sixDigitCode);
                DateTime expiresAtUtc = now.AddMinutes(InvitationLifetimeMinutes);

                EnrolmentInvitation invitation = new()
                {
                    Id = Guid.NewGuid(),
                    ServerPersonId = serverPersonId,
                    QrCodeHash = hashedQrCode.Hash,
                    QrCodeSalt = hashedQrCode.Salt,
                    SixDigitHash = hashedSixDigitCode.Hash,
                    SixDigitSalt = hashedSixDigitCode.Salt,
                    CodeIterations = hashedQrCode.Iterations,
                    CodeAlgorithm = hashedQrCode.Algorithm,
                    FailedSixDigitAttempts = 0,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = expiresAtUtc,
                    ConsumedAtUtc = null,
                    ConsumedByDeviceId = null,
                };

                _dbContext.EnrolmentInvitations.Add(invitation);
                await _dbContext.SaveChangesAsync(transactionCancellationToken);

                return new TransactionOutcome<EnrolmentInvitationCreated>
                {
                    Value = new EnrolmentInvitationCreated(invitation.Id, qrCodeValue, sixDigitCode, expiresAtUtc),
                    ShouldCommit = true,
                };
            },
            cancellationToken);
    }

    public Task<EnrolmentRedemptionResult> RedeemAsync(
        EnrolmentRedemptionRequest request,
        CancellationToken cancellationToken)
    {
        return _transactionRunner.RunAsync(
            _dbContext,
            async transactionCancellationToken => await RedeemInsideTransactionAsync(request, transactionCancellationToken),
            cancellationToken);
    }

    private async Task<TransactionOutcome<EnrolmentRedemptionResult>> RedeemInsideTransactionAsync(
        EnrolmentRedemptionRequest request,
        CancellationToken cancellationToken)
    {
        DateTime now = _clock.UtcNow;
        EnrolmentInvitation? invitation = await LoadOutstandingInvitationAsync(now, cancellationToken);

        if (invitation is null)
        {
            return Rejected(EnrolmentRedemptionOutcome.CodeExpired);
        }

        if (request.Code is not null)
        {
            bool qrCodeMatches = _secretHasher.Verify(
                request.Code,
                invitation.QrCodeHash,
                invitation.QrCodeSalt,
                invitation.CodeIterations,
                invitation.CodeAlgorithm);

            return qrCodeMatches
                ? await CompleteRedemptionAsync(invitation, request, now, cancellationToken)
                : Rejected(EnrolmentRedemptionOutcome.CodeInvalid);
        }

        if (request.SixDigitCode is null)
        {
            return Rejected(EnrolmentRedemptionOutcome.CodeInvalid);
        }

        if (invitation.FailedSixDigitAttempts >= MaximumFailedSixDigitAttempts)
        {
            return Rejected(EnrolmentRedemptionOutcome.SixDigitAttemptsExhausted);
        }

        bool sixDigitCodeMatches = _secretHasher.Verify(
            request.SixDigitCode,
            invitation.SixDigitHash,
            invitation.SixDigitSalt,
            invitation.CodeIterations,
            invitation.CodeAlgorithm);

        if (!sixDigitCodeMatches)
        {
            invitation.FailedSixDigitAttempts++;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new TransactionOutcome<EnrolmentRedemptionResult>
            {
                Value = new EnrolmentRedemptionResult(EnrolmentRedemptionOutcome.CodeInvalid, null, null),
                ShouldCommit = true,
            };
        }

        return await CompleteRedemptionAsync(invitation, request, now, cancellationToken);
    }

    private async Task<EnrolmentInvitation?> LoadOutstandingInvitationAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        EnrolmentInvitation? invitation = await _dbContext.EnrolmentInvitations
            .FirstOrDefaultAsync(
                candidate => candidate.ConsumedAtUtc == null && candidate.ExpiresAtUtc > now,
                cancellationToken);

        if (invitation is null)
        {
            return null;
        }

        await _dbContext.Entry(invitation).ReloadAsync(cancellationToken);

        return invitation.ConsumedAtUtc is null && invitation.ExpiresAtUtc > now ? invitation : null;
    }

    private TransactionOutcome<EnrolmentRedemptionResult> Rejected(EnrolmentRedemptionOutcome outcome)
    {
        return new TransactionOutcome<EnrolmentRedemptionResult>
        {
            Value = new EnrolmentRedemptionResult(outcome, null, null),
            ShouldCommit = false,
        };
    }

    private async Task<TransactionOutcome<EnrolmentRedemptionResult>> CompleteRedemptionAsync(
        EnrolmentInvitation invitation,
        EnrolmentRedemptionRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ServerPerson serverPerson = await ResolveServerPersonAsync(invitation, request.Name, now, cancellationToken);

        await RevokeEarlierDevicesAsync(serverPerson.Id, cancellationToken);

        IssuedDeviceToken issued = await _deviceTokenStore.IssueAsync(
            serverPerson.Id,
            ResolveLanguage(request.AcceptLanguageHeader),
            request.UserAgent,
            cancellationToken);

        invitation.ConsumedAtUtc = now;
        invitation.ConsumedByDeviceId = issued.Device.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TransactionOutcome<EnrolmentRedemptionResult>
        {
            Value = new EnrolmentRedemptionResult(EnrolmentRedemptionOutcome.Redeemed, issued.Device, serverPerson),
            ShouldCommit = true,
        };
    }

    private async Task ConsumeOutstandingInvitationsAsync(DateTime now, CancellationToken cancellationToken)
    {
        List<EnrolmentInvitation> outstanding = await _dbContext.EnrolmentInvitations
            .Where(invitation => invitation.ConsumedAtUtc == null && invitation.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (EnrolmentInvitation invitation in outstanding)
        {
            invitation.ConsumedAtUtc = now;
        }

        if (outstanding.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RevokeEarlierDevicesAsync(Guid serverPersonId, CancellationToken cancellationToken)
    {
        List<Device> earlierDevices = await _dbContext.Devices
            .Where(device => device.ServerPersonId == serverPersonId && device.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (Device device in earlierDevices)
        {
            await _deviceTokenStore.RevokeAsync(device.Id, cancellationToken);
        }
    }

    private async Task<ServerPerson> ResolveServerPersonAsync(
        EnrolmentInvitation invitation,
        string name,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (invitation.ServerPersonId is not null)
        {
            ServerPerson existingPerson = await _dbContext.ServerPeople
                .SingleAsync(person => person.Id == invitation.ServerPersonId, cancellationToken);
            existingPerson.Name = name;

            return existingPerson;
        }

        ServerPerson newPerson = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAtUtc = now,
        };

        _dbContext.ServerPeople.Add(newPerson);

        return newPerson;
    }

    private string ResolveLanguage(string acceptLanguageHeader)
    {
        string firstTag = acceptLanguageHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(string.Empty);

        return firstTag.StartsWith(EnglishLanguage, StringComparison.OrdinalIgnoreCase)
            ? EnglishLanguage
            : GermanLanguage;
    }
}
