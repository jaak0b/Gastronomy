using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class TicketAcknowledgePolicyTest
{
    private TicketAcknowledgePolicy _policy = new();

    [SetUp]
    public void SetUp()
    {
        _policy = new TicketAcknowledgePolicy();
    }

    private StationPrintability HealthyStation()
    {
        return new StationPrintability
        {
            IsFaulty = false,
            IsOnline = true,
            IsPaperEnd = false,
            IsCoverOpen = false,
            IsInErrorState = false,
            IsEnabled = true,
        };
    }

    private List<StationPrintability> StationsThatCannotPrint()
    {
        return
        [
            HealthyStation() with { IsFaulty = true },
            HealthyStation() with { IsOnline = false },
            HealthyStation() with { IsPaperEnd = true },
            HealthyStation() with { IsCoverOpen = true },
            HealthyStation() with { IsInErrorState = true },
            HealthyStation() with { IsEnabled = false },
        ];
    }

    [Test]
    public void CanAcknowledge_AFailedTicketAtAHealthyStation_IsAllowed()
    {
        Assert.That(_policy.CanAcknowledge(LocationTicketStatus.Failed, HealthyStation()), Is.True);
    }

    [Test]
    public void CanAcknowledge_AnUnknownTicketAtAHealthyStation_IsAllowed()
    {
        Assert.That(_policy.CanAcknowledge(LocationTicketStatus.Unknown, HealthyStation()), Is.True);
    }

    [Test]
    public void CanAcknowledge_ABlockedTicketAtAHealthyStation_IsAllowed()
    {
        Assert.That(_policy.CanAcknowledge(LocationTicketStatus.Blocked, HealthyStation()), Is.True);
    }

    [Test]
    public void CanAcknowledge_AFailedUnknownOrBlockedTicket_IsAllowedUnderEveryStationCondition()
    {
        LocationTicketStatus[] alwaysAllowed =
        [
            LocationTicketStatus.Failed,
            LocationTicketStatus.Unknown,
            LocationTicketStatus.Blocked,
        ];

        foreach (LocationTicketStatus ticketStatus in alwaysAllowed)
        {
            Assert.That(_policy.CanAcknowledge(ticketStatus, HealthyStation()), Is.True, $"{ticketStatus}");

            foreach (StationPrintability station in StationsThatCannotPrint())
            {
                Assert.That(_policy.CanAcknowledge(ticketStatus, station), Is.True, $"{ticketStatus}");
            }
        }
    }

    [Test]
    public void CanAcknowledge_AQueuedTicketAtAFaultyStation_IsAllowed()
    {
        Assert.That(
            _policy.CanAcknowledge(LocationTicketStatus.Queued, HealthyStation() with { IsFaulty = true }),
            Is.True);
    }

    [Test]
    public void CanAcknowledge_AQueuedTicketAtAnOfflineStation_IsAllowed()
    {
        Assert.That(
            _policy.CanAcknowledge(LocationTicketStatus.Queued, HealthyStation() with { IsOnline = false }),
            Is.True);
    }

    [Test]
    public void CanAcknowledge_AQueuedTicketAtAStationOutOfPaper_IsAllowed()
    {
        Assert.That(
            _policy.CanAcknowledge(LocationTicketStatus.Queued, HealthyStation() with { IsPaperEnd = true }),
            Is.True);
    }

    [Test]
    public void CanAcknowledge_AQueuedTicketAtAStationWithAnOpenCover_IsAllowed()
    {
        Assert.That(
            _policy.CanAcknowledge(LocationTicketStatus.Queued, HealthyStation() with { IsCoverOpen = true }),
            Is.True);
    }

    [Test]
    public void CanAcknowledge_AQueuedTicketAtAStationInAnErrorState_IsAllowed()
    {
        Assert.That(
            _policy.CanAcknowledge(LocationTicketStatus.Queued, HealthyStation() with { IsInErrorState = true }),
            Is.True);
    }

    [Test]
    public void CanAcknowledge_AQueuedTicketAtADisabledStation_IsAllowed()
    {
        Assert.That(
            _policy.CanAcknowledge(LocationTicketStatus.Queued, HealthyStation() with { IsEnabled = false }),
            Is.True);
    }

    [Test]
    public void CanAcknowledge_AQueuedTicketAtAHealthyStation_IsRefused()
    {
        Assert.That(_policy.CanAcknowledge(LocationTicketStatus.Queued, HealthyStation()), Is.False);
    }

    [Test]
    public void CanAcknowledge_APrintingTicketAtAHealthyStation_IsRefused()
    {
        Assert.That(_policy.CanAcknowledge(LocationTicketStatus.Printing, HealthyStation()), Is.False);
    }

    [Test]
    public void CanAcknowledge_APrintingTicket_IsRefusedUnderEveryStationCondition()
    {
        foreach (StationPrintability station in StationsThatCannotPrint())
        {
            Assert.That(_policy.CanAcknowledge(LocationTicketStatus.Printing, station), Is.False);
        }
    }

    [Test]
    public void CanAcknowledge_AFinishedTicketAtAHealthyStation_IsRefused()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_policy.CanAcknowledge(LocationTicketStatus.Printed, HealthyStation()), Is.False);
            Assert.That(
                _policy.CanAcknowledge(LocationTicketStatus.PrintedOnTestPrinter, HealthyStation()),
                Is.False);
            Assert.That(_policy.CanAcknowledge(LocationTicketStatus.HandledOnPaper, HealthyStation()), Is.False);
        });
    }
}
