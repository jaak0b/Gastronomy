using System.Security.Cryptography;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
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
  private readonly DeviceOwnerStore _ownerStore;
  private readonly Pbkdf2SecretHasher _secretHasher;
  private readonly ImmediateTransactionRunner _transactionRunner = new();

  public EnrolmentInvitationStore(GastronomyAppDbContext dbContext,
                                  DeviceOwnerStore ownerStore,
                                  Pbkdf2SecretHasher secretHasher,
                                  IDeviceTokenStore deviceTokenStore,
                                  IClock clock)
  {
    _dbContext = dbContext;
    _ownerStore = ownerStore;
    _secretHasher = secretHasher;
    _deviceTokenStore = deviceTokenStore;
    _clock = clock;
  }

  public Task<EnrolmentInvitationCreated> CreateAsync(DeviceOwner? owner, CancellationToken cancellationToken)
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

                                         if (owner is not null)
                                         {
                                           await _ownerStore.PointInvitationAsync(owner, invitation.Id, transactionCancellationToken);
                                           await _dbContext.SaveChangesAsync(transactionCancellationToken);
                                         }

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
    var invitation = await LoadUnconsumedInvitationAsync(cancellationToken);

    if (invitation is null)
    {
      return Rejected(EnrolmentRedemptionOutcome.NoInvitationOutstanding, null);
    }

    if (invitation.ExpiresAtUtc <= now)
    {
      return Rejected(EnrolmentRedemptionOutcome.CodeExpired, invitation.Id);
    }

    var qrCodeMatches = _secretHasher.Verify(request.Code,
                                             invitation.QrCodeHash,
                                             invitation.QrCodeSalt,
                                             invitation.QrCodeIterations,
                                             invitation.QrCodeAlgorithm);

    if (!qrCodeMatches)
    {
      return Rejected(EnrolmentRedemptionOutcome.CodeInvalid, invitation.Id);
    }

    var owner = await _ownerStore.FindByInvitationAsync(invitation.Id, cancellationToken);

    if (owner is null)
    {
      if (string.IsNullOrWhiteSpace(request.Name))
      {
        return Rejected(EnrolmentRedemptionOutcome.NameRequired, invitation.Id);
      }

      owner = await CreateStaffMemberAsync(request.Name, now, cancellationToken);
    }
    else
    {
      var ownerRecord = await _ownerStore.FindAsync(owner, cancellationToken);

      if (ownerRecord is null || !ownerRecord.IsActive)
      {
        return Rejected(OffTheListOutcomeFor(owner.Kind), invitation.Id);
      }
    }

    return await CompleteRedemptionAsync(invitation, owner, request, now, cancellationToken);
  }

  private EnrolmentRedemptionOutcome OffTheListOutcomeFor(DeviceOwnerKind kind)
  {
    return kind switch
           {
             DeviceOwnerKind.StaffMember => EnrolmentRedemptionOutcome.StaffMemberIsOffTheList,
             DeviceOwnerKind.Station => EnrolmentRedemptionOutcome.StationIsOffTheList,
             _ => new Core.Services.Never().OfType<EnrolmentRedemptionOutcome>(kind)
           };
  }

  private async Task<EnrolmentInvitation?> LoadUnconsumedInvitationAsync(CancellationToken cancellationToken)
  {
    var invitation = await _dbContext.EnrolmentInvitations
                                     .FirstOrDefaultAsync(candidate => candidate.ConsumedAtUtc == null,
                                                          cancellationToken);

    if (invitation is null)
    {
      return null;
    }

    await _dbContext.Entry(invitation).ReloadAsync(cancellationToken);

    return invitation.ConsumedAtUtc is null ? invitation : null;
  }

  private TransactionOutcome<EnrolmentRedemptionResult> Rejected(EnrolmentRedemptionOutcome outcome,
                                                                 Guid? invitationId)
  {
    return new()
           {
             Value = new(outcome, null, null, null, null, null, invitationId),
             ShouldCommit = false
           };
  }

  private async Task<TransactionOutcome<EnrolmentRedemptionResult>> CompleteRedemptionAsync(EnrolmentInvitation invitation,
                                                                                            DeviceOwner owner,
                                                                                            EnrolmentRedemptionRequest request,
                                                                                            DateTime now,
                                                                                            CancellationToken cancellationToken)
  {
    var issued = await _deviceTokenStore.IssueAsync(owner,
                                                    ResolveLanguage(request.AcceptLanguageHeader),
                                                    request.UserAgent,
                                                    cancellationToken);

    invitation.ConsumedAtUtc = now;
    invitation.ConsumedByDeviceId = issued.Device.Id;
    await _ownerStore.PointInvitationAsync(owner, null, cancellationToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    var staffMember = owner.Kind == DeviceOwnerKind.StaffMember
                        ? await _dbContext.StaffMembers.SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken)
                        : null;
    var station = owner.Kind == DeviceOwnerKind.Station
                    ? await _dbContext.Stations.SingleAsync(candidate => candidate.Id == owner.Id, cancellationToken)
                    : null;

    return new()
           {
             Value = new(EnrolmentRedemptionOutcome.Redeemed,
                         owner.Kind,
                         issued.Device,
                         staffMember,
                         station,
                         issued.PlaintextToken,
                         invitation.Id),
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
      await _ownerStore.ForgetInvitationsAsync([.. unconsumedPredecessors.Select(predecessor => predecessor.Id)],
                                               cancellationToken);
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  private async Task<DeviceOwner> CreateStaffMemberAsync(string name, DateTime now, CancellationToken cancellationToken)
  {
    StaffMember newStaffMember = new()
                                 {
                                   Id = Guid.NewGuid(),
                                   Name = name.Trim(),
                                   IsActive = true,
                                   CreatedAtUtc = now
                                 };

    _dbContext.StaffMembers.Add(newStaffMember);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new(DeviceOwnerKind.StaffMember, newStaffMember.Id);
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
