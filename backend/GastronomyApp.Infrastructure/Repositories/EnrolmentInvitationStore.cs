using System.Security.Cryptography;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using GastronomyApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class EnrolmentInvitationStore : IEnrolmentInvitationStore
{
  private const int QRCodeLengthBytes = 32;
  private const int InvitationLifetimeMinutes = 5;
  private const string GermanLanguage = "de";
  private const string EnglishLanguage = "en";
  private readonly IClock _clock;

  private readonly GastronomyAppDbContext _dbContext;
  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly IDeviceOwnerStore _ownerStore;
  private readonly Pbkdf2SecretHasher _secretHasher;
  private readonly ITransactionRunner _transactionRunner;

  public EnrolmentInvitationStore(GastronomyAppDbContext dbContext, IDeviceOwnerStore ownerStore, Pbkdf2SecretHasher secretHasher, IDeviceTokenStore deviceTokenStore, ITransactionRunner transactionRunner, IClock clock)
  {
    _dbContext = dbContext;
    _transactionRunner = transactionRunner;
    _ownerStore = ownerStore;
    _secretHasher = secretHasher;
    _deviceTokenStore = deviceTokenStore;
    _clock = clock;
  }

  public Task<IssuedEnrolmentInvitation> CreateAsync(IDeviceOwner? owner, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(async transactionCancellationToken =>
                                       {
                                         var now = _clock.UtcNow;

                                         await ConsumeEveryUnconsumedPredecessorIncludingExpiredOnesAsync(now, transactionCancellationToken);

                                         var qrCodeValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(QRCodeLengthBytes));

                                         var hashedQRCode = _secretHasher.Hash(qrCodeValue);
                                         var expiresAtUtc = now.AddMinutes(InvitationLifetimeMinutes);

                                         EnrolmentInvitation invitation = new()
                                                                          {
                                                                            Id = Guid.NewGuid(),
                                                                            QRCodeHash = hashedQRCode.Hash,
                                                                            QRCodeSalt = hashedQRCode.Salt,
                                                                            QRCodeIterations = hashedQRCode.Iterations,
                                                                            QRCodeAlgorithm = hashedQRCode.Algorithm,
                                                                            CreatedAtUtc = now,
                                                                            ExpiresAtUtc = expiresAtUtc,
                                                                            ConsumedAtUtc = null,
                                                                            ConsumedByDeviceId = null
                                                                          };

                                         _dbContext.EnrolmentInvitations.Add(invitation);
                                         await _dbContext.SaveChangesAsync(transactionCancellationToken);

                                         if (owner is not null)
                                         {
                                           owner.EnrolmentInvitationId = invitation.Id;
                                           owner.EnrolmentInvitation = invitation;
                                           await _dbContext.SaveChangesAsync(transactionCancellationToken);
                                         }

                                         return new TransactionOutcome<IssuedEnrolmentInvitation>
                                                {
                                                  Value = new(invitation, qrCodeValue, owner),
                                                  ShouldCommit = true
                                                };
                                       },
                                       cancellationToken);
  }

  public Task<EnrolmentRedemptionResult> RedeemAsync(string code, string? name, string userAgent, string acceptLanguageHeader, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(async transactionCancellationToken => await RedeemInsideTransactionAsync(code, name, userAgent, acceptLanguageHeader, transactionCancellationToken), cancellationToken);
  }

  public async Task<EnrolmentInvitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken)
  {
    return await _dbContext.EnrolmentInvitations.AsNoTracking().FirstOrDefaultAsync(invitation => invitation.Id == invitationId, cancellationToken);
  }

  public async Task ConsumeAsync(Guid invitationId, DateTime consumedAtUtc, CancellationToken cancellationToken)
  {
    var invitation = await _dbContext.EnrolmentInvitations.FirstOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);

    if (invitation is not null && invitation.ConsumedAtUtc is null)
      invitation.ConsumedAtUtc = consumedAtUtc;
  }

  private async Task<TransactionOutcome<EnrolmentRedemptionResult>> RedeemInsideTransactionAsync(string code, string? name, string userAgent, string acceptLanguageHeader, CancellationToken cancellationToken)
  {
    var now = _clock.UtcNow;
    var invitation = await LoadUnconsumedInvitationAsync(cancellationToken);

    if (invitation is null)
      return Rejected(EnrolmentRedemptionOutcome.NoInvitationOutstanding, null);

    if (invitation.ExpiresAtUtc <= now)
      return Rejected(EnrolmentRedemptionOutcome.CodeExpired, invitation);

    var qrCodeMatches = _secretHasher.Verify(code, invitation.QRCodeHash, invitation.QRCodeSalt, invitation.QRCodeIterations, invitation.QRCodeAlgorithm);

    if (!qrCodeMatches)
      return Rejected(EnrolmentRedemptionOutcome.CodeInvalid, invitation);

    var owner = await _ownerStore.FindByInvitationAsync(invitation.Id, cancellationToken);

    if (owner is null)
    {
      if (string.IsNullOrWhiteSpace(name))
        return Rejected(EnrolmentRedemptionOutcome.NameRequired, invitation);

      owner = await CreateStaffMemberAsync(name, now, cancellationToken);
    }
    else if (!owner.IsActive)
      return Rejected(OffTheListOutcomeFor(owner.Kind), invitation);

    return await CompleteRedemptionAsync(invitation, owner, userAgent, acceptLanguageHeader, now, cancellationToken);
  }

  private EnrolmentRedemptionOutcome OffTheListOutcomeFor(DeviceOwnerKind kind)
  {
    return kind switch
           {
             DeviceOwnerKind.StaffMember => EnrolmentRedemptionOutcome.StaffMemberIsOffTheList,
             DeviceOwnerKind.Station => EnrolmentRedemptionOutcome.StationIsOffTheList,
             _ => new UnreachableCase().Throw<EnrolmentRedemptionOutcome>(kind)
           };
  }

  private async Task<EnrolmentInvitation?> LoadUnconsumedInvitationAsync(CancellationToken cancellationToken)
  {
    var invitation = await _dbContext.EnrolmentInvitations.FirstOrDefaultAsync(candidate => candidate.ConsumedAtUtc == null, cancellationToken);

    if (invitation is null)
      return null;

    await _dbContext.Entry(invitation).ReloadAsync(cancellationToken);

    if (invitation.ConsumedAtUtc is null)
      return invitation;

    return null;
  }

  private TransactionOutcome<EnrolmentRedemptionResult> Rejected(EnrolmentRedemptionOutcome outcome, EnrolmentInvitation? invitation)
  {
    return new()
           {
             Value = new(outcome, invitation, null, null),
             ShouldCommit = false
           };
  }

  private async Task<TransactionOutcome<EnrolmentRedemptionResult>> CompleteRedemptionAsync(EnrolmentInvitation invitation, IDeviceOwner owner, string userAgent, string acceptLanguageHeader, DateTime now, CancellationToken cancellationToken)
  {
    var issued = await _deviceTokenStore.IssueAsync(owner, ResolveLanguage(acceptLanguageHeader), userAgent, cancellationToken);

    invitation.ConsumedAtUtc = now;
    invitation.ConsumedByDeviceId = issued.Device.Id;
    owner.EnrolmentInvitationId = null;
    owner.EnrolmentInvitation = null;
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new()
           {
             Value = new(EnrolmentRedemptionOutcome.Redeemed, invitation, owner, issued.PlaintextToken),
             ShouldCommit = true
           };
  }

  private async Task ConsumeEveryUnconsumedPredecessorIncludingExpiredOnesAsync(DateTime now, CancellationToken cancellationToken)
  {
    List<EnrolmentInvitation> unconsumedPredecessors = await _dbContext.EnrolmentInvitations.Where(invitation => invitation.ConsumedAtUtc == null).ToListAsync(cancellationToken);

    foreach (var predecessor in unconsumedPredecessors)
      predecessor.ConsumedAtUtc = now;

    if (unconsumedPredecessors.Count > 0)
    {
      await _ownerStore.ForgetInvitationsAsync(unconsumedPredecessors.Select(predecessor => predecessor.Id).ToList(), cancellationToken);
      await _dbContext.SaveChangesAsync(cancellationToken);
    }
  }

  private async Task<IDeviceOwner> CreateStaffMemberAsync(string name, DateTime now, CancellationToken cancellationToken)
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

    return newStaffMember;
  }

  private string ResolveLanguage(string acceptLanguageHeader)
  {
    var firstTag = acceptLanguageHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(string.Empty);

    if (firstTag.StartsWith(GermanLanguage, StringComparison.OrdinalIgnoreCase))
      return GermanLanguage;

    return EnglishLanguage;
  }
}
