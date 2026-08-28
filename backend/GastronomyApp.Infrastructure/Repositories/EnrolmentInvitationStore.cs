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
  private const int InvitationLifetimeMinutes = 5;
  private const string GermanLanguage = "de";
  private const string EnglishLanguage = "en";
  private readonly IClock _clock;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly Pbkdf2SecretHasher _secretHasher;
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public EnrolmentInvitationStore(GastronomyAppDbContext dbContext,
                                  Pbkdf2SecretHasher secretHasher,
                                  IDeviceTokenStore deviceTokenStore,
                                  IClock clock)
  {
    _dbContext = dbContext;
    _secretHasher = secretHasher;
    _deviceTokenStore = deviceTokenStore;
    _clock = clock;
  }

  public Task<EnrolmentInvitationCreated> CreateAsync(Guid? staffMemberId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(_dbContext,
                                       async transactionCancellationToken =>
                                       {
                                         var now = _clock.UtcNow;

                                         await ConsumeEveryUnconsumedPredecessorIncludingExpiredOnesAsync(now, transactionCancellationToken);

                                         var qrCodeValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(QrCodeLengthBytes));

                                         var hashedQrCode = _secretHasher.Hash(qrCodeValue);
                                         var expiresAtUtc = now.AddMinutes(InvitationLifetimeMinutes);

                                         EnrolmentInvitation invitation = new()
                                                                          {
                                                                            Id = Guid.NewGuid(),
                                                                            StaffMemberId = staffMemberId,
                                                                            QrCodeHash = hashedQrCode.Hash,
                                                                            QrCodeSalt = hashedQrCode.Salt,
                                                                            QrCodeIterations = hashedQrCode.Iterations,
                                                                            QrCodeAlgorithm = hashedQrCode.Algorithm,
                                                                            CreatedAtUtc = now,
                                                                            ExpiresAtUtc = expiresAtUtc,
                                                                            ConsumedAtUtc = null,
                                                                            ConsumedByDeviceId = null
                                                                          };

                                         _dbContext.EnrolmentInvitations.Add(invitation);
                                         await _dbContext.SaveChangesAsync(transactionCancellationToken);

                                         return new TransactionOutcome<EnrolmentInvitationCreated>
                                                {
                                                  Value = new(invitation.Id, qrCodeValue, expiresAtUtc),
                                                  ShouldCommit = true
                                                };
                                       },
                                       cancellationToken);
  }

  public Task<EnrolmentRedemptionResult> RedeemAsync(EnrolmentRedemptionRequest request,
                                                     CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(_dbContext,
                                       async transactionCancellationToken => await RedeemInsideTransactionAsync(request, transactionCancellationToken),
                                       cancellationToken);
  }

  private async Task<TransactionOutcome<EnrolmentRedemptionResult>> RedeemInsideTransactionAsync(EnrolmentRedemptionRequest request,
                                                                                                 CancellationToken cancellationToken)
  {
    var now = _clock.UtcNow;
    var invitation = await LoadOutstandingInvitationAsync(now, cancellationToken);

    if (invitation is null)
    {
      return Rejected(EnrolmentRedemptionOutcome.CodeExpired);
    }

    var qrCodeMatches = _secretHasher.Verify(request.Code,
                                             invitation.QrCodeHash,
                                             invitation.QrCodeSalt,
                                             invitation.QrCodeIterations,
                                             invitation.QrCodeAlgorithm);

    if (!qrCodeMatches)
    {
      return Rejected(EnrolmentRedemptionOutcome.CodeInvalid);
    }

    if (invitation.StaffMemberId is not null)
    {
      var staffMemberIsOnTheList = await _dbContext.StaffMembers.AnyAsync(staffMember => staffMember.Id == invitation.StaffMemberId && staffMember.IsActive,
                                                                          cancellationToken);

      if (!staffMemberIsOnTheList)
      {
        return Rejected(EnrolmentRedemptionOutcome.StaffMemberIsOffTheList);
      }
    }
    else if (string.IsNullOrWhiteSpace(request.Name))
    {
      return Rejected(EnrolmentRedemptionOutcome.NameRequired);
    }

    return await CompleteRedemptionAsync(invitation, request, now, cancellationToken);
  }

  private async Task<EnrolmentInvitation?> LoadOutstandingInvitationAsync(DateTime now,
                                                                          CancellationToken cancellationToken)
  {
    var invitation = await _dbContext.EnrolmentInvitations
                                     .FirstOrDefaultAsync(candidate => candidate.ConsumedAtUtc == null && candidate.ExpiresAtUtc > now,
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
    return new()
           {
             Value = new(outcome, null, null, null),
             ShouldCommit = false
           };
  }

  private async Task<TransactionOutcome<EnrolmentRedemptionResult>> CompleteRedemptionAsync(EnrolmentInvitation invitation,
                                                                                            EnrolmentRedemptionRequest request,
                                                                                            DateTime now,
                                                                                            CancellationToken cancellationToken)
  {
    var staffMember = await ResolveStaffMemberAsync(invitation, request.Name, now, cancellationToken);

    await RevokeEarlierDevicesAsync(staffMember.Id, cancellationToken);

    var issued = await _deviceTokenStore.IssueAsync(staffMember.Id,
                                                    ResolveLanguage(request.AcceptLanguageHeader),
                                                    request.UserAgent,
                                                    cancellationToken);

    invitation.ConsumedAtUtc = now;
    invitation.ConsumedByDeviceId = issued.Device.Id;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new()
           {
             Value = new(EnrolmentRedemptionOutcome.Redeemed,
                         issued.Device,
                         staffMember,
                         issued.PlaintextToken),
             ShouldCommit = true
           };
  }

  private async Task ConsumeEveryUnconsumedPredecessorIncludingExpiredOnesAsync(DateTime now,
                                                                                CancellationToken cancellationToken)
  {
    List<EnrolmentInvitation> unconsumedPredecessors = await _dbContext.EnrolmentInvitations
                                                                       .Where(invitation => invitation.ConsumedAtUtc == null)
                                                                       .ToListAsync(cancellationToken);

    foreach (var predecessor in unconsumedPredecessors)
    {
      predecessor.ConsumedAtUtc = now;
    }

    if (unconsumedPredecessors.Count > 0)
    {
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  private async Task RevokeEarlierDevicesAsync(Guid staffMemberId, CancellationToken cancellationToken)
  {
    List<Device> earlierDevices = await _dbContext.Devices
                                                  .Where(device => device.StaffMemberId == staffMemberId)
                                                  .ToListAsync(cancellationToken);

    foreach (var device in earlierDevices)
    {
      await _deviceTokenStore.RevokeAsync(device.Id, cancellationToken);
    }
  }

  private async Task<StaffMember> ResolveStaffMemberAsync(EnrolmentInvitation invitation,
                                                          string? name,
                                                          DateTime now,
                                                          CancellationToken cancellationToken)
  {
    if (invitation.StaffMemberId is not null)
    {
      return await _dbContext.StaffMembers
                             .SingleAsync(staffMember => staffMember.Id == invitation.StaffMemberId, cancellationToken);
    }

    StaffMember newStaffMember = new()
                                 {
                                   Id = Guid.NewGuid(),
                                   Name = name!,
                                   IsActive = true,
                                   CreatedAtUtc = now
                                 };

    _dbContext.StaffMembers.Add(newStaffMember);

    return newStaffMember;
  }

  private string ResolveLanguage(string acceptLanguageHeader)
  {
    var firstTag = acceptLanguageHeader
                  .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .FirstOrDefault(string.Empty);

    return firstTag.StartsWith(EnglishLanguage, StringComparison.OrdinalIgnoreCase)
             ? EnglishLanguage
             : GermanLanguage;
  }
}
