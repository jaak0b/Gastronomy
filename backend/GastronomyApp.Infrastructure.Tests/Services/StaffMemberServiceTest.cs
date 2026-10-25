using FakeItEasy;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class StaffMemberServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _invitationStore = A.Fake<IEnrolmentInvitationStore>();
    _deviceTokenStore = A.Fake<IDeviceTokenStore>();
    _announcer = A.Fake<IDeviceRevocationAnnouncer>();
    var afterCommitActions = A.Fake<IAfterCommitActions>();
    A.CallTo(() => afterCommitActions.RunWhenCommittedAsync(A<Func<CancellationToken, Task>>._, A<CancellationToken>._)).ReturnsLazily(call => ((Func<CancellationToken, Task>)call.Arguments[0]!)(call.GetArgument<CancellationToken>(1)));
    FakeTimeProvider clock = new(new(_now));
    var mapping = new CoreMappingFactory().Create();
    _service = new(_fixture.DbContext, mapping, new(_invitationStore, _deviceTokenStore, _announcer, afterCommitActions, clock));
    _activation = new(_fixture.DbContext, mapping);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  private ActivationService _activation = null!;
  private IDeviceRevocationAnnouncer _announcer = null!;
  private IDeviceTokenStore _deviceTokenStore = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private IEnrolmentInvitationStore _invitationStore = null!;
  private SeededDomain _seeded = null!;
  private StaffMemberService _service = null!;

  [Test]
  public async Task ListAsync_WaitersStoredOutOfOrder_ReadsThemByNameWithTheirFields()
  {
    var berndId = await AddStaffMemberAsync("Bernd", true, true, false);
    await AddStaffMemberAsync("Zora", false, false, false);
    _fixture.DbContext.ChangeTracker.Clear();

    var listed = await _service.ListAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(listed.StaffMembers.Select(staffMember => staffMember.Name),
                                  Is.EqualTo(new[]
                                             {
                                               "Anna",
                                               "Bernd",
                                               "Zora"
                                             }));
                      Assert.That(listed.StaffMembers[0].StaffMemberId, Is.EqualTo(_seeded.StaffMemberId));
                      Assert.That(listed.StaffMembers[0].IsActive, Is.True);
                      Assert.That(listed.StaffMembers[0].HasDevice, Is.False);
                      Assert.That(listed.StaffMembers[1].StaffMemberId, Is.EqualTo(berndId));
                      Assert.That(listed.StaffMembers[1].HasDevice, Is.True);
                      Assert.That(listed.StaffMembers[2].IsActive, Is.False);
                    });
  }

  [Test]
  public async Task RenameAsync_ASomebodyWhoIsNotOnTheList_FailsBecauseThePersonIsNotFound()
  {
    var renamed = await _service.RenameAsync(Guid.NewGuid(), new() { Name = "Annemarie" }, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(renamed.IsError, Is.True);
                      Assert.That(renamed.RefusalMessageKey(), Is.EqualTo("StaffMemberNotFound"));
                    });
  }

  [Test]
  public async Task RenameAsync_ANewName_KeepsExactlyWhatWasTypedAndStoresIt()
  {
    var renamed = await _service.RenameAsync(_seeded.StaffMemberId, new() { Name = "Anne Marie" }, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(renamed.IsError, Is.False);
                      Assert.That(renamed.Value, Is.EqualTo(new StaffMemberView(_seeded.StaffMemberId, "Anne Marie")));
                      Assert.That(stored.Name, Is.EqualTo("Anne Marie"));
                    });
  }

  [Test]
  public async Task ActivateAsync_ASomebodyWhoWasTakenOffTheList_PutsThemBackOnIt()
  {
    var berndId = await AddStaffMemberAsync("Bernd", false, false, false);

    var switchedOn = await _activation.ActivateAsync<StaffMember, StaffMemberView>(berndId, Refusal.StaffMember.StaffMemberNotFound, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == berndId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOn.IsError, Is.False);
                      Assert.That(switchedOn.Value, Is.EqualTo(new StaffMemberView(berndId, "Bernd")));
                      Assert.That(stored.IsActive, Is.True);
                    });
  }

  [Test]
  public async Task ActivateAsync_ASomebodyWhoIsNotOnTheList_FailsBecauseThePersonIsNotFound()
  {
    var switchedOn = await _activation.ActivateAsync<StaffMember, StaffMemberView>(Guid.NewGuid(), Refusal.StaffMember.StaffMemberNotFound, TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOn.RefusalMessageKey(), Is.EqualTo("StaffMemberNotFound"));
  }

  [Test]
  public async Task DeactivateAsync_ASomebodyWhoIsNotOnTheList_FailsBecauseThePersonIsNotFound()
  {
    var switchedOff = await _service.DeactivateAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOff.RefusalMessageKey(), Is.EqualTo("StaffMemberNotFound"));
  }

  [Test]
  public async Task DeactivateAsync_ASomebodyHoldingAPhone_WithdrawsTheInvitationAndRevokesThePhone()
  {
    var berndId = await AddStaffMemberAsync("Bernd", true, true, true);
    var bernd = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == berndId, TestContext.CurrentContext.CancellationToken);
    var deviceId = bernd.DeviceId!.Value;
    var invitationId = bernd.EnrolmentInvitationId!.Value;

    var switchedOff = await _service.DeactivateAsync(berndId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var stored = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == berndId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(switchedOff.IsError, Is.False);
                      Assert.That(switchedOff.Value, Is.EqualTo(new StaffMemberView(berndId, "Bernd")));
                      Assert.That(stored.IsActive, Is.False);
                      Assert.That(stored.EnrolmentInvitationId, Is.Null);
                    });

    A.CallTo(() => _invitationStore.ConsumeAsync(invitationId, _now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _deviceTokenStore.RevokeAsync(deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _announcer.AnnounceDeviceRevokedAsync(deviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task DeactivateAsync_ASomebodyWithoutAPhone_RevokesNothing()
  {
    var switchedOff = await _service.DeactivateAsync(_seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.That(switchedOff.IsError, Is.False);

    A.CallTo(() => _invitationStore.ConsumeAsync(A<Guid>._, A<DateTime>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _deviceTokenStore.RevokeAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _announcer.AnnounceDeviceRevokedAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private async Task<Guid> AddStaffMemberAsync(string name, bool isActive, bool withDevice, bool withInvitation)
  {
    StaffMember staffMember = new()
                              {
                                Id = Guid.NewGuid(),
                                Name = name,
                                IsActive = isActive,
                                CreatedAtUtc = _now
                              };

    if (withDevice)
    {
      Device device = new()
                      {
                        Id = Guid.NewGuid(),
                        Language = "de",
                        TokenHash = [1],
                        TokenSalt = [1],
                        TokenIterations = 1,
                        TokenAlgorithm = "PBKDF2",
                        TokenLookupId = Guid.NewGuid().ToString(),
                        CreatedAtUtc = _now
                      };
      _fixture.DbContext.Devices.Add(device);
      staffMember.DeviceId = device.Id;
    }

    if (withInvitation)
    {
      EnrolmentInvitation invitation = new()
                                       {
                                         Id = Guid.NewGuid(),
                                         QRCodeHash = [1],
                                         QRCodeSalt = [1],
                                         QRCodeIterations = 1,
                                         QRCodeAlgorithm = "PBKDF2",
                                         CreatedAtUtc = _now,
                                         ExpiresAtUtc = _now.AddHours(1)
                                       };
      _fixture.DbContext.EnrolmentInvitations.Add(invitation);
      staffMember.EnrolmentInvitationId = invitation.Id;
    }

    _fixture.DbContext.StaffMembers.Add(staffMember);
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return staffMember.Id;
  }
}
