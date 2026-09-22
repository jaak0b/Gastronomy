using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class EnrolmentInvitationRulesTest
{
  private readonly EnrolmentInvitationRules _invitationRules = new();
  private readonly DateTime _now = new(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc);

  [Test]
  public void IsOutstandingAt_NobodyScannedItAndItHasTimeLeft_IsTrue()
  {
    Assert.That(_invitationRules.IsOutstandingAt(InvitationWith(_now.AddMinutes(5), null), _now), Is.True);
  }

  [Test]
  public void IsOutstandingAt_ADeviceAlreadyScannedIt_IsFalse()
  {
    Assert.That(_invitationRules.IsOutstandingAt(InvitationWith(_now.AddMinutes(5), _now.AddMinutes(-1)), _now), Is.False);
  }

  [Test]
  public void IsOutstandingAt_ItsTimeRanOut_IsFalse()
  {
    Assert.That(_invitationRules.IsOutstandingAt(InvitationWith(_now.AddMinutes(-1), null), _now), Is.False);
  }

  [Test]
  public void IsOutstandingAt_ItRunsOutAtThisVeryMoment_IsFalse()
  {
    Assert.That(_invitationRules.IsOutstandingAt(InvitationWith(_now, null), _now), Is.False);
  }

  private static EnrolmentInvitation InvitationWith(DateTime expiresAtUtc, DateTime? consumedAtUtc)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             QRCodeHash = [1],
             QRCodeSalt = [2],
             QRCodeIterations = 1,
             QRCodeAlgorithm = "SHA512",
             CreatedAtUtc = new(2026, 9, 5, 17, 55, 0, DateTimeKind.Utc),
             ExpiresAtUtc = expiresAtUtc,
             ConsumedAtUtc = consumedAtUtc
           };
  }
}
