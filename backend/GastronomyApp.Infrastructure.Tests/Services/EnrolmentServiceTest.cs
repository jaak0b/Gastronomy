using FakeItEasy;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Security;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class EnrolmentServiceTest
{
  [SetUp]
  public async Task SetUp()
  {
    _fixture = new();
    _seeded = await new DomainSeeder().SeedAsync(_fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    _clock = new(new(2026, 8, 27, 18, 0, 0, TimeSpan.Zero));
    _composition = new(_fixture.DbContext, _clock);
  }

  [TearDown]
  public void TearDown()
  {
    _fixture.Dispose();
  }

  private FakeTimeProvider _clock = null!;
  private EnrolmentComposition _composition = null!;
  private SqliteInMemoryFixture _fixture = null!;
  private SeededDomain _seeded = null!;

  [Test]
  public async Task CreateInvitationAsync_BothAWaiterAndAStation_FailsBecauseOnlyOneOwnerIsAllowed()
  {
    var created = await _composition.Service.CreateInvitationAsync(_seeded.StaffMemberId, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    var invitationCount = await _fixture.DbContext.EnrolmentInvitations.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsError, Is.True);
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("errors.enrolment.atMostOneOwner"));
                      Assert.That(invitationCount, Is.Zero);
                    });
  }

  [Test]
  public async Task CreateInvitationAsync_AnOwnerWhoIsNotOnTheList_FailsBecauseTheOwnerIsNotFound()
  {
    var created = await _composition.Service.CreateInvitationAsync(Guid.NewGuid(), null, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsError, Is.True);
                      Assert.That(created.RefusalMessageKey(), Is.EqualTo("OwnerNotFound"));
                    });
  }

  [Test]
  public async Task CreateInvitationAsync_NobodyNamed_IssuesAnInvitationWithoutAnOwnerAndRemembersItsCode()
  {
    var created = await _composition.Service.CreateInvitationAsync(null, null, TestContext.CurrentContext.CancellationToken);
    var remembered = _composition.Cache.Read();

    Assert.Multiple(() =>
                    {
                      Assert.That(created.IsError, Is.False);
                      Assert.That(created.Value.StaffMember, Is.Null);
                      Assert.That(created.Value.Station, Is.Null);
                      Assert.That(remembered, Is.Not.Null);
                      Assert.That(created.Value.InvitationId, Is.EqualTo(remembered!.InvitationId));
                      Assert.That(created.Value.QRUrl, Is.EqualTo($"http://192.168.0.5:5000/j/{remembered.QRCodeValue}"));
                      Assert.That(created.Value.ExpiresAtUtc, Is.EqualTo(_clock.GetUtcNow().UtcDateTime.AddMinutes(5)));
                    });
  }

  [Test]
  public async Task CreateInvitationAsync_AWaiter_PointsTheWaiterAtTheInvitationAndNamesTheWaiter()
  {
    var created = await _composition.Service.CreateInvitationAsync(_seeded.StaffMemberId, null, TestContext.CurrentContext.CancellationToken);
    var anna = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.StaffMember!.Name, Is.EqualTo("Anna"));
                      Assert.That(anna.EnrolmentInvitationId, Is.EqualTo(created.Value.InvitationId));
                    });
  }

  [Test]
  public async Task CreateInvitationAsync_AStationThatAlreadyHoldsATablet_RevokesThatTabletAndTellsTheLaptop()
  {
    var oldDeviceId = await GiveTheKitchenATabletAsync();

    var created = await _composition.Service.CreateInvitationAsync(null, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var oldDeviceStillStored = await _fixture.DbContext.Devices.AnyAsync(device => device.Id == oldDeviceId, TestContext.CurrentContext.CancellationToken);
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(created.Value.Station!.Name, Is.EqualTo("Kueche"));
                      Assert.That(oldDeviceStillStored, Is.False);
                      Assert.That(kitchen.DeviceId, Is.Null);
                    });

    A.CallTo(() => _composition.RevocationAnnouncer.AnnounceDeviceRevokedAsync(oldDeviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task CreateInvitationAsync_Twice_ConsumesTheFirstOutstandingInvitation()
  {
    var first = await _composition.Service.CreateInvitationAsync(_seeded.StaffMemberId, null, TestContext.CurrentContext.CancellationToken);
    var second = await _composition.Service.CreateInvitationAsync(_seeded.StaffMemberId, null, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var firstRow = await _fixture.DbContext.EnrolmentInvitations.SingleAsync(invitation => invitation.Id == first.Value.InvitationId, TestContext.CurrentContext.CancellationToken);
    var secondRow = await _fixture.DbContext.EnrolmentInvitations.SingleAsync(invitation => invitation.Id == second.Value.InvitationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(firstRow.ConsumedAtUtc, Is.Not.Null);
                      Assert.That(secondRow.ConsumedAtUtc, Is.Null);
                    });
  }

  [Test]
  public async Task CreateInvitationAsync_ANewInvitationForAnotherOwner_TakesTheReplacedInvitationAwayFromTheFirstOwner()
  {
    var forAnna = await _composition.Service.CreateInvitationAsync(_seeded.StaffMemberId, null, TestContext.CurrentContext.CancellationToken);
    var forTheKitchen = await _composition.Service.CreateInvitationAsync(null, _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var anna = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(forAnna.Value.InvitationId, Is.Not.EqualTo(forTheKitchen.Value.InvitationId));
                      Assert.That(anna.EnrolmentInvitationId, Is.Null);
                      Assert.That(kitchen.EnrolmentInvitationId, Is.EqualTo(forTheKitchen.Value.InvitationId));
                    });
  }

  [Test]
  public async Task CreateInvitationAsync_AfterAnEarlierInvitationExpiredUnredeemed_ConsumesItAndSucceeds()
  {
    var yesterday = await _composition.Service.CreateInvitationAsync(_seeded.StaffMemberId, null, TestContext.CurrentContext.CancellationToken);
    _clock.Advance(TimeSpan.FromDays(1));
    var today = await _composition.Service.CreateInvitationAsync(_seeded.StaffMemberId, null, TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var expiredRow = await _fixture.DbContext.EnrolmentInvitations.SingleAsync(invitation => invitation.Id == yesterday.Value.InvitationId, TestContext.CurrentContext.CancellationToken);
    var currentRow = await _fixture.DbContext.EnrolmentInvitations.SingleAsync(invitation => invitation.Id == today.Value.InvitationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(expiredRow.ConsumedAtUtc, Is.Not.Null, "An expired invitation still holds the single outstanding marker until it is consumed.");
                      Assert.That(currentRow.ConsumedAtUtc, Is.Null);
                    });
  }

  [Test]
  public void RedeemAsync_NullCode_ThrowsArgumentNullException()
  {
    Assert.That(async () => await _composition.Service.RedeemAsync(new() { Code = null }, "de", TestContext.CurrentContext.CancellationToken), Throws.ArgumentNullException);
  }

  [Test]
  public async Task RedeemAsync_TheCodeOfAnInvitationForAWaiter_EnrolsThatWaiterWithTheLanguageOfTheBrowser()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "de-DE,de;q=0.9", TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var anna = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redeemed.IsError, Is.False);
                      Assert.That(redeemed.Value.StaffMember!.Name, Is.EqualTo("Anna"));
                      Assert.That(redeemed.Value.Station, Is.Null);
                      Assert.That(redeemed.Value.Language, Is.EqualTo("de"));
                      Assert.That(redeemed.Value.DeviceId, Is.EqualTo(anna.DeviceId));
                      Assert.That(anna.EnrolmentInvitationId, Is.Null);
                    });
  }

  [Test]
  public async Task RedeemAsync_ABrowserThatAsksForAnotherLanguage_GetsEnglish()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "fr-FR,fr;q=0.9", TestContext.CurrentContext.CancellationToken);

    Assert.That(redeemed.Value.Language, Is.EqualTo("en"));
  }

  [Test]
  public async Task RedeemAsync_InvitationForNobodyAndATypedName_EnrolsANewActiveWaiter()
  {
    var code = await CreateInvitationCodeAsync(null, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, "Bernd"), "de", TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var bernd = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Name == "Bernd", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redeemed.IsError, Is.False);
                      Assert.That(redeemed.Value.StaffMember!.Id, Is.EqualTo(bernd.Id));
                      Assert.That(bernd.IsActive, Is.True);
                      Assert.That(bernd.DeviceId, Is.EqualTo(redeemed.Value.DeviceId));
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForNobodyAndABlankName_AsksForTheNameAndKeepsTheInvitationOpen()
  {
    var code = await CreateInvitationCodeAsync(null, null);

    var refused = await _composition.Service.RedeemAsync(RequestFor(code, "   "), "de", TestContext.CurrentContext.CancellationToken);
    var stillOpen = await _composition.Service.RedeemAsync(RequestFor(code, "Bernd"), "de", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(refused.RefusalMessageKey(), Is.EqualTo("errors.enrolment.nameMissing"));
                      Assert.That(stillOpen.IsError, Is.False);
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForAStation_IssuesAStationDeviceAndClearsTheInvitationPointer()
  {
    var code = await CreateInvitationCodeAsync(null, _seeded.KitchenStationId);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redeemed.IsError, Is.False);
                      Assert.That(redeemed.Value.Station!.Id, Is.EqualTo(_seeded.KitchenStationId));
                      Assert.That(redeemed.Value.StaffMember, Is.Null);
                      Assert.That(kitchen.DeviceId, Is.EqualTo(redeemed.Value.DeviceId));
                      Assert.That(kitchen.EnrolmentInvitationId, Is.Null);
                    });
  }

  [Test]
  public async Task RedeemAsync_InvitationForAStationThatWasSwitchedOff_IsRejectedAsOffTheList()
  {
    var code = await CreateInvitationCodeAsync(null, _seeded.KitchenStationId);
    var kitchen = await _fixture.DbContext.Stations.SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);

    Assert.That(redeemed.RefusalMessageKey(), Is.EqualTo("errors.enrolment.stationIsOffTheList"));
  }

  [Test]
  public async Task RedeemAsync_InvitationForAWaiterWhoWasTakenOffTheList_IsRejected()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    var anna = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    anna.IsActive = false;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);

    Assert.That(redeemed.RefusalMessageKey(), Is.EqualTo("errors.enrolment.staffMemberIsOffTheList"));
  }

  [Test]
  public async Task RedeemAsync_ACodeThatRanOut_IsRefusedAndStoresNoDevice()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    _clock.Advance(TimeSpan.FromMinutes(6));

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);
    var deviceCount = await _fixture.DbContext.Devices.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redeemed.RefusalMessageKey(), Is.EqualTo("errors.enrolment.codeNoLongerValid"));
                      Assert.That(deviceCount, Is.Zero);
                    });

    A.CallTo(() => _composition.CompletionAnnouncer.AnnounceEnrolmentCompletedAsync(A<IDeviceOwner>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task RedeemAsync_AWrongCode_IsRefusedAsUnknownAndStoresNoDevice()
  {
    await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor("not-the-right-code", null), "de", TestContext.CurrentContext.CancellationToken);
    var deviceCount = await _fixture.DbContext.Devices.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redeemed.RefusalMessageKey(), Is.EqualTo("errors.enrolment.codeUnknown"));
                      Assert.That(deviceCount, Is.Zero);
                    });
  }

  [Test]
  public async Task RedeemAsync_ACodeThatWasAlreadyScanned_IsRefusedTheSecondTime()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var first = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);
    var second = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);
    var deviceCount = await _fixture.DbContext.Devices.CountAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(first.IsError, Is.False);
                      Assert.That(second.RefusalMessageKey(), Is.EqualTo("errors.enrolment.codeNoLongerValid"));
                      Assert.That(deviceCount, Is.EqualTo(1));
                    });
  }

  [Test]
  public async Task RedeemAsync_TheCodeOfAnInvitationThatANewerOneReplaced_IsRefused()
  {
    var oldCode = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(oldCode, null), "de", TestContext.CurrentContext.CancellationToken);

    Assert.That(redeemed.RefusalMessageKey(), Is.EqualTo("errors.enrolment.codeUnknown"));
  }

  [Test]
  public async Task RedeemAsync_AWaiterWithAnEarlierPhone_LeavesOnlyTheNewDeviceAndSignsTheOldTokenOut()
  {
    var firstCode = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    var first = await _composition.Service.RedeemAsync(RequestFor(firstCode, null), "de", TestContext.CurrentContext.CancellationToken);
    var secondCode = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var second = await _composition.Service.RedeemAsync(RequestFor(secondCode, null), "de", TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var anna = await _fixture.DbContext.StaffMembers.SingleAsync(staffMember => staffMember.Id == _seeded.StaffMemberId, TestContext.CurrentContext.CancellationToken);
    var deviceCount = await _fixture.DbContext.Devices.CountAsync(TestContext.CurrentContext.CancellationToken);
    var oldToken = SplitToken(first.Value.DeviceToken);
    var oldTokenOwner = await _composition.Lookup.FindByTokenAsync(oldToken.Lookup, oldToken.Secret, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(second.IsError, Is.False);
                      Assert.That(deviceCount, Is.EqualTo(1));
                      Assert.That(anna.DeviceId, Is.EqualTo(second.Value.DeviceId));
                      Assert.That(anna.DeviceId, Is.Not.EqualTo(first.Value.DeviceId));
                      Assert.That(oldTokenOwner, Is.Null);
                    });
  }

  [Test]
  public async Task RedeemAsync_ASuccessfulRedemption_ReturnsATokenThatIdentifiesTheOwner()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);
    var token = SplitToken(redeemed.Value.DeviceToken);
    var owner = await _composition.Lookup.FindByTokenAsync(token.Lookup, token.Secret, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(owner, Is.Not.Null);
                      Assert.That(owner!.Id, Is.EqualTo(_seeded.StaffMemberId));
                      Assert.That(owner.Device!.Id, Is.EqualTo(redeemed.Value.DeviceId));
                    });
  }

  [Test]
  public async Task RedeemAsync_ASuccessfulRedemption_NeverStoresThePlaintextTokenOrCode()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);
    var token = SplitToken(redeemed.Value.DeviceToken);

    List<string> storedValues = [];

    foreach (var table in new[]
                          {
                            "Devices",
                            "EnrolmentInvitations"
                          })
    {
      using var command = _fixture.Connection.CreateCommand();
      command.CommandText = $"SELECT * FROM {table}";
      using var reader = await command.ExecuteReaderAsync(TestContext.CurrentContext.CancellationToken);

      while (await reader.ReadAsync(TestContext.CurrentContext.CancellationToken))
        for (var column = 0; column < reader.FieldCount; column++)
        {
          var value = reader.GetValue(column);

          if (value is byte[] bytes)
            storedValues.Add(Convert.ToHexString(bytes));
          else
            storedValues.Add(value.ToString() ?? string.Empty);
        }
    }

    Assert.Multiple(() =>
                    {
                      Assert.That(storedValues, Is.Not.Empty);
                      Assert.That(storedValues.Any(value => value.Contains(token.Secret, StringComparison.Ordinal)), Is.False);
                      Assert.That(storedValues.Any(value => value.Contains(redeemed.Value.DeviceToken, StringComparison.Ordinal)), Is.False);
                      Assert.That(storedValues.Any(value => value.Contains(code, StringComparison.Ordinal)), Is.False);
                    });
  }

  [Test]
  public async Task RedeemAsync_AnInvitationThePhoneScanned_RecordsTheDeviceOnTheInvitationForgetsTheCodeAndTellsTheLaptopOnce()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var invitation = await _fixture.DbContext.EnrolmentInvitations.SingleAsync(TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(invitation.ConsumedAtUtc, Is.EqualTo(_clock.GetUtcNow().UtcDateTime));
                      Assert.That(invitation.ConsumedByDeviceId, Is.EqualTo(redeemed.Value.DeviceId));
                      Assert.That(_composition.Cache.ForgetCount, Is.EqualTo(1));
                    });

    A.CallTo(() => _composition.CompletionAnnouncer.AnnounceEnrolmentCompletedAsync(A<IDeviceOwner>.That.Matches(owner => owner.Id == _seeded.StaffMemberId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RedeemAsync_AnotherDeviceTheBrowserStillHeld_SignsThatOneOut()
  {
    var kitchenDeviceId = await GiveTheKitchenATabletAsync();
    var previousToken = await GiveTheKitchenAKnownTokenAsync();
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null, previousToken), "de", TestContext.CurrentContext.CancellationToken);
    _fixture.DbContext.ChangeTracker.Clear();

    var kitchenDeviceStillStored = await _fixture.DbContext.Devices.AnyAsync(device => device.Id == kitchenDeviceId, TestContext.CurrentContext.CancellationToken);
    var newDeviceStored = await _fixture.DbContext.Devices.AnyAsync(device => device.Id == redeemed.Value.DeviceId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(redeemed.IsError, Is.False);
                      Assert.That(kitchenDeviceStillStored, Is.False);
                      Assert.That(newDeviceStored, Is.True);
                    });

    A.CallTo(() => _composition.RevocationAnnouncer.AnnounceDeviceRevokedAsync(kitchenDeviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Test]
  public async Task RedeemAsync_APreviousTokenThatDoesNotVerify_RevokesNothing()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null, "lookup.secret"), "de", TestContext.CurrentContext.CancellationToken);

    Assert.That(redeemed.IsError, Is.False);

    A.CallTo(() => _composition.RevocationAnnouncer.AnnounceDeviceRevokedAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task RedeemAsync_APreviousTokenOfTheSameOwner_AnnouncesNoRevocationBeyondTheOneTheNewInvitationCaused()
  {
    var firstCode = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    var first = await _composition.Service.RedeemAsync(RequestFor(firstCode, null), "de", TestContext.CurrentContext.CancellationToken);
    var secondCode = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var second = await _composition.Service.RedeemAsync(RequestFor(secondCode, null, first.Value.DeviceToken), "de", TestContext.CurrentContext.CancellationToken);

    Assert.That(second.IsError, Is.False);

    A.CallTo(() => _composition.RevocationAnnouncer.AnnounceDeviceRevokedAsync(first.Value.DeviceId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _composition.RevocationAnnouncer.AnnounceDeviceRevokedAsync(second.Value.DeviceId, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Test]
  public async Task ReadOpenQRUrlAsync_AnInvitationNobodyKnows_FailsBecauseTheInvitationIsUnknown()
  {
    var qrUrl = await _composition.Service.ReadOpenQRUrlAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(qrUrl.IsError, Is.True);
                      Assert.That(qrUrl.RefusalMessageKey(), Is.EqualTo("errors.enrolment.qrUnavailable"));
                    });
  }

  [Test]
  public async Task ReadOpenQRUrlAsync_AnInvitationAPhoneAlreadyUsed_SaysItWasAlreadyUsed()
  {
    var code = await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    var invitationId = _composition.Cache.Read()!.InvitationId;
    await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);

    var qrUrl = await _composition.Service.ReadOpenQRUrlAsync(invitationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(qrUrl.RefusalMessageKey(), Is.EqualTo("errors.enrolment.qrAlreadyUsed"));
  }

  [Test]
  public async Task ReadOpenQRUrlAsync_AnInvitationANewerOneReplaced_SaysItWasReplaced()
  {
    await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    var replacedId = _composition.Cache.Read()!.InvitationId;
    await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);

    var qrUrl = await _composition.Service.ReadOpenQRUrlAsync(replacedId, TestContext.CurrentContext.CancellationToken);

    Assert.That(qrUrl.RefusalMessageKey(), Is.EqualTo("errors.enrolment.qrReplaced"));
  }

  [Test]
  public async Task ReadOpenQRUrlAsync_AnInvitationWhoseTimeRanOut_SaysItExpired()
  {
    await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    var invitationId = _composition.Cache.Read()!.InvitationId;
    _clock.Advance(TimeSpan.FromMinutes(5));

    var qrUrl = await _composition.Service.ReadOpenQRUrlAsync(invitationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(qrUrl.RefusalMessageKey(), Is.EqualTo("errors.enrolment.qrExpired"));
  }

  [Test]
  public async Task ReadOpenQRUrlAsync_AnInvitationTheLaptopNoLongerHolds_SaysThePictureIsUnavailable()
  {
    await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    var invitationId = _composition.Cache.Read()!.InvitationId;
    _composition.Cache.ForgetInvitation();

    var qrUrl = await _composition.Service.ReadOpenQRUrlAsync(invitationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(qrUrl.RefusalMessageKey(), Is.EqualTo("errors.enrolment.qrUnavailable"));
  }

  [Test]
  public async Task ReadOpenQRUrlAsync_AnInvitationThatIsStillOutstanding_AnswersWithTheRememberedAddress()
  {
    await CreateInvitationCodeAsync(_seeded.StaffMemberId, null);
    var remembered = _composition.Cache.Read()!;

    var qrUrl = await _composition.Service.ReadOpenQRUrlAsync(remembered.InvitationId, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(qrUrl.IsError, Is.False);
                      Assert.That(qrUrl.Value, Is.EqualTo(remembered.QRUrl));
                    });
  }

  private async Task<string> CreateInvitationCodeAsync(Guid? staffMemberId, Guid? stationId)
  {
    var created = await _composition.Service.CreateInvitationAsync(staffMemberId, stationId, TestContext.CurrentContext.CancellationToken);

    Assert.That(created.IsError, Is.False);

    return _composition.Cache.Read()!.QRCodeValue;
  }

  private RedeemEnrolmentRequest RequestFor(string code, string? name, string? previousDeviceToken = null)
  {
    return new()
           {
             Code = code,
             Name = name,
             PreviousDeviceToken = previousDeviceToken
           };
  }

  private TokenParts SplitToken(string plaintextToken)
  {
    var separatorIndex = plaintextToken.IndexOf('.', StringComparison.Ordinal);

    return new(plaintextToken[..separatorIndex], plaintextToken[(separatorIndex + 1)..]);
  }

  private async Task<Guid> GiveTheKitchenATabletAsync()
  {
    var code = await CreateInvitationCodeAsync(null, _seeded.KitchenStationId);
    var redeemed = await _composition.Service.RedeemAsync(RequestFor(code, null), "de", TestContext.CurrentContext.CancellationToken);

    return redeemed.Value.DeviceId;
  }

  private async Task<string> GiveTheKitchenAKnownTokenAsync()
  {
    const string secret = "known-secret";
    var kitchen = await _fixture.DbContext.Stations.Include(station => station.Device).SingleAsync(station => station.Id == _seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    var device = kitchen.Device!;
    var hashed = new Pbkdf2SecretHasher().Hash(secret);

    device.TokenHash = hashed.Hash;
    device.TokenSalt = hashed.Salt;
    device.TokenIterations = hashed.Iterations;
    device.TokenAlgorithm = hashed.Algorithm;
    await _fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    return $"{device.TokenLookupId}.{secret}";
  }

  private sealed record TokenParts(string Lookup, string Secret);
}
