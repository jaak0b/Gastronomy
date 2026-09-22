using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class StaffMemberServiceTest
{
  private readonly StaffMemberService _staffMemberService = new();

  [Test]
  public void HasOutstandingInvitation_NobodyWasInvited_IsFalse()
  {
    var staffMember = StaffMemberWith(null);

    Assert.That(_staffMemberService.HasOutstandingInvitation(staffMember), Is.False);
  }

  [Test]
  public void HasOutstandingInvitation_AnInvitationIsAttached_IsTrue()
  {
    var staffMember = StaffMemberWith(new()
                                      {
                                        Id = Guid.NewGuid(),
                                        QRCodeHash = [1],
                                        QRCodeSalt = [2],
                                        QRCodeIterations = 1,
                                        QRCodeAlgorithm = "SHA512",
                                        CreatedAtUtc = new(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc),
                                        ExpiresAtUtc = new(2026, 9, 5, 18, 5, 0, DateTimeKind.Utc)
                                      });

    Assert.That(_staffMemberService.HasOutstandingInvitation(staffMember), Is.True);
  }

  private static StaffMember StaffMemberWith(EnrolmentInvitation? invitation)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = "Anna",
             IsActive = true,
             CreatedAtUtc = new(2026, 9, 5, 17, 0, 0, DateTimeKind.Utc),
             EnrolmentInvitation = invitation
           };
  }
}
