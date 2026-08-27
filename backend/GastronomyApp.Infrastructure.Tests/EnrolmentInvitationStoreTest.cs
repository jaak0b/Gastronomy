using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Ports;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Security;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class EnrolmentInvitationStoreTest
{
    [Test]
    public async Task CreateAsync_ThenRedeemAsync_WithReturnedQrCode_Redeems()
    {
        using SqliteInMemoryFixture fixture = new();
        EnrolmentInvitationStore store = CreateStore(fixture, new AdjustableClock());

        EnrolmentInvitationCreated created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        EnrolmentRedemptionResult redemption = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(created.QrCodeValue, null, "Anna", "Test agent", "de-DE,de;q=0.9"),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.Redeemed));
            Assert.That(redemption.Device, Is.Not.Null);
            Assert.That(redemption.ServerPerson!.Name, Is.EqualTo("Anna"));
            Assert.That(redemption.Device!.Language, Is.EqualTo("de"));
        });
    }

    [Test]
    public async Task CreateAsync_ThenRedeemAsync_WithReturnedSixDigitCode_Redeems()
    {
        using SqliteInMemoryFixture fixture = new();
        EnrolmentInvitationStore store = CreateStore(fixture, new AdjustableClock());

        EnrolmentInvitationCreated created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        EnrolmentRedemptionResult redemption = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(null, created.SixDigitCode, "Bernd", "Test agent", "en-GB,en;q=0.9"),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.Redeemed));
            Assert.That(redemption.Device!.Language, Is.EqualTo("en"));
        });
    }

    [Test]
    public async Task CreateAsync_Twice_ConsumesTheFirstOutstandingInvitation()
    {
        using SqliteInMemoryFixture fixture = new();
        EnrolmentInvitationStore store = CreateStore(fixture, new AdjustableClock());

        EnrolmentInvitationCreated first = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        EnrolmentInvitationCreated second = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);

        EnrolmentInvitation firstRow = await fixture.DbContext.EnrolmentInvitations
            .SingleAsync(invitation => invitation.Id == first.InvitationId, TestContext.CurrentContext.CancellationToken);
        EnrolmentInvitation secondRow = await fixture.DbContext.EnrolmentInvitations
            .SingleAsync(invitation => invitation.Id == second.InvitationId, TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(firstRow.ConsumedAtUtc, Is.Not.Null);
            Assert.That(secondRow.ConsumedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task RedeemAsync_WrongSixDigitCode_TenTimes_ExhaustsAttempts()
    {
        using SqliteInMemoryFixture fixture = new();
        EnrolmentInvitationStore store = CreateStore(fixture, new AdjustableClock());

        EnrolmentInvitationCreated created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        string wrongCode = WrongSixDigitCodeFor(created.SixDigitCode);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            EnrolmentRedemptionResult rejected = await store.RedeemAsync(
                new EnrolmentRedemptionRequest(null, wrongCode, "Anna", "Test agent", "de"),
                TestContext.CurrentContext.CancellationToken);
            Assert.That(rejected.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.CodeInvalid));
        }

        EnrolmentRedemptionResult exhausted = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(null, created.SixDigitCode, "Anna", "Test agent", "de"),
            TestContext.CurrentContext.CancellationToken);

        Assert.That(exhausted.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.SixDigitAttemptsExhausted));
    }

    [Test]
    public async Task RedeemAsync_WrongSixDigitCode_TenthAttempt_QrCodeStillVerifies()
    {
        using SqliteInMemoryFixture fixture = new();
        EnrolmentInvitationStore store = CreateStore(fixture, new AdjustableClock());

        EnrolmentInvitationCreated created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        string wrongCode = WrongSixDigitCodeFor(created.SixDigitCode);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            await store.RedeemAsync(
                new EnrolmentRedemptionRequest(null, wrongCode, "Anna", "Test agent", "de"),
                TestContext.CurrentContext.CancellationToken);
        }

        EnrolmentRedemptionResult redemption = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(created.QrCodeValue, null, "Anna", "Test agent", "de"),
            TestContext.CurrentContext.CancellationToken);

        Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.Redeemed));
    }

    [Test]
    public async Task RedeemAsync_ExpiredInvitation_IsRejected()
    {
        using SqliteInMemoryFixture fixture = new();
        AdjustableClock clock = new();
        EnrolmentInvitationStore store = CreateStore(fixture, clock);

        EnrolmentInvitationCreated created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(6));

        EnrolmentRedemptionResult redemption = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(created.QrCodeValue, null, "Anna", "Test agent", "de"),
            TestContext.CurrentContext.CancellationToken);

        Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.CodeExpired));
    }

    [Test]
    public async Task RedeemAsync_PersonWithAnEarlierPhone_RevokesThatEarlierDevice()
    {
        using SqliteInMemoryFixture fixture = new();
        AdjustableClock clock = new();
        EnrolmentInvitationStore store = CreateStore(fixture, clock);

        EnrolmentInvitationCreated firstInvitation = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        EnrolmentRedemptionResult firstRedemption = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(firstInvitation.QrCodeValue, null, "Anna", "Old phone", "de"),
            TestContext.CurrentContext.CancellationToken);

        Guid serverPersonId = firstRedemption.ServerPerson!.Id;
        Guid oldDeviceId = firstRedemption.Device!.Id;

        EnrolmentInvitationCreated secondInvitation = await store.CreateAsync(serverPersonId, TestContext.CurrentContext.CancellationToken);
        EnrolmentRedemptionResult secondRedemption = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(secondInvitation.QrCodeValue, null, "Anna", "New phone", "de"),
            TestContext.CurrentContext.CancellationToken);

        Device oldDevice = await fixture.DbContext.Devices
            .SingleAsync(device => device.Id == oldDeviceId, TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(secondRedemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.Redeemed));
            Assert.That(secondRedemption.ServerPerson!.Id, Is.EqualTo(serverPersonId));
            Assert.That(oldDevice.RevokedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task CreateAsync_TwoConcurrentCallers_LeaveExactlyOneOutstandingInvitation()
    {
        using SqliteTempFileFixture fixture = new();
        AdjustableClock clock = new();

        EnrolmentInvitationStore firstStore = CreateStore(fixture.CreateContext(), clock);
        EnrolmentInvitationStore secondStore = CreateStore(fixture.CreateContext(), clock);

        await Task.WhenAll(
            Task.Run(() => firstStore.CreateAsync(null, TestContext.CurrentContext.CancellationToken)),
            Task.Run(() => secondStore.CreateAsync(null, TestContext.CurrentContext.CancellationToken)));

        GastronomyAppDbContext verificationContext = fixture.CreateContext();
        int outstandingCount = await verificationContext.EnrolmentInvitations
            .CountAsync(
                invitation => invitation.ConsumedAtUtc == null && invitation.ExpiresAtUtc > clock.UtcNow,
                TestContext.CurrentContext.CancellationToken);

        Assert.That(outstandingCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RedeemAsync_TwoConcurrentCallers_IssueExactlyOneDevice()
    {
        using SqliteTempFileFixture fixture = new();
        AdjustableClock clock = new();

        EnrolmentInvitationCreated created = await CreateStore(fixture.CreateContext(), clock)
            .CreateAsync(null, TestContext.CurrentContext.CancellationToken);

        EnrolmentInvitationStore firstStore = CreateStore(fixture.CreateContext(), clock);
        EnrolmentInvitationStore secondStore = CreateStore(fixture.CreateContext(), clock);

        EnrolmentRedemptionResult[] results = await Task.WhenAll(
            Task.Run(() => firstStore.RedeemAsync(
                new EnrolmentRedemptionRequest(created.QrCodeValue, null, "Anna", "First phone", "de"),
                TestContext.CurrentContext.CancellationToken)),
            Task.Run(() => secondStore.RedeemAsync(
                new EnrolmentRedemptionRequest(created.QrCodeValue, null, "Anna", "Second phone", "de"),
                TestContext.CurrentContext.CancellationToken)));

        GastronomyAppDbContext verificationContext = fixture.CreateContext();
        int deviceCount = await verificationContext.Devices
            .CountAsync(TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(
                results.Count(result => result.Outcome == EnrolmentRedemptionOutcome.Redeemed),
                Is.EqualTo(1));
            Assert.That(deviceCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task RedeemAsync_SuccessfulRedemption_CarriesAPlaintextTokenThatVerifies()
    {
        using SqliteInMemoryFixture fixture = new();
        AdjustableClock clock = new();
        Pbkdf2SecretHasher secretHasher = new();
        DeviceTokenStore deviceTokenStore = new(fixture.DbContext, secretHasher, clock);
        EnrolmentInvitationStore store = new(fixture.DbContext, secretHasher, deviceTokenStore, clock);

        EnrolmentInvitationCreated created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        EnrolmentRedemptionResult redemption = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(created.QrCodeValue, null, "Anna", "Test agent", "de"),
            TestContext.CurrentContext.CancellationToken);

        Assert.That(redemption.PlaintextToken, Is.Not.Null);

        int separatorIndex = redemption.PlaintextToken!.IndexOf('.', StringComparison.Ordinal);
        DeviceVerificationResult verification = await deviceTokenStore.VerifyAsync(
            redemption.PlaintextToken[..separatorIndex],
            redemption.PlaintextToken[(separatorIndex + 1)..],
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(verification.IsValid, Is.True);
            Assert.That(verification.Device!.Id, Is.EqualTo(redemption.Device!.Id));
        });
    }

    [Test]
    public async Task RedeemAsync_WrongQrCode_CarriesNoPlaintextToken()
    {
        using SqliteInMemoryFixture fixture = new();
        EnrolmentInvitationStore store = CreateStore(fixture, new AdjustableClock());

        await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        EnrolmentRedemptionResult redemption = await store.RedeemAsync(
            new EnrolmentRedemptionRequest("not-the-right-code", null, "Anna", "Test agent", "de"),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.CodeInvalid));
            Assert.That(redemption.PlaintextToken, Is.Null);
        });
    }

    [Test]
    public async Task RedeemAsync_ExpiredInvitation_CarriesNoPlaintextToken()
    {
        using SqliteInMemoryFixture fixture = new();
        AdjustableClock clock = new();
        EnrolmentInvitationStore store = CreateStore(fixture, clock);

        EnrolmentInvitationCreated created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(6));

        EnrolmentRedemptionResult redemption = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(created.QrCodeValue, null, "Anna", "Test agent", "de"),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(redemption.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.CodeExpired));
            Assert.That(redemption.PlaintextToken, Is.Null);
        });
    }

    [Test]
    public async Task RedeemAsync_SixDigitAttemptsExhausted_CarriesNoPlaintextToken()
    {
        using SqliteInMemoryFixture fixture = new();
        EnrolmentInvitationStore store = CreateStore(fixture, new AdjustableClock());

        EnrolmentInvitationCreated created = await store.CreateAsync(null, TestContext.CurrentContext.CancellationToken);
        string wrongCode = WrongSixDigitCodeFor(created.SixDigitCode);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            EnrolmentRedemptionResult rejected = await store.RedeemAsync(
                new EnrolmentRedemptionRequest(null, wrongCode, "Anna", "Test agent", "de"),
                TestContext.CurrentContext.CancellationToken);
            Assert.That(rejected.PlaintextToken, Is.Null);
        }

        EnrolmentRedemptionResult exhausted = await store.RedeemAsync(
            new EnrolmentRedemptionRequest(null, created.SixDigitCode, "Anna", "Test agent", "de"),
            TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(exhausted.Outcome, Is.EqualTo(EnrolmentRedemptionOutcome.SixDigitAttemptsExhausted));
            Assert.That(exhausted.PlaintextToken, Is.Null);
        });
    }

    private string WrongSixDigitCodeFor(string correctSixDigitCode)
    {
        return correctSixDigitCode == "000000" ? "111111" : "000000";
    }

    private EnrolmentInvitationStore CreateStore(SqliteInMemoryFixture fixture, AdjustableClock clock)
    {
        return CreateStore(fixture.DbContext, clock);
    }

    private EnrolmentInvitationStore CreateStore(GastronomyAppDbContext dbContext, AdjustableClock clock)
    {
        Pbkdf2SecretHasher secretHasher = new();

        return new EnrolmentInvitationStore(
            dbContext,
            secretHasher,
            new DeviceTokenStore(dbContext, secretHasher, clock),
            clock);
    }
}
