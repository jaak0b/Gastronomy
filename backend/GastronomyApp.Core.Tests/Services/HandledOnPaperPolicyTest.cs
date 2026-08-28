using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class HandledOnPaperPolicyTest
{
  private HandledOnPaperPolicy _policy = new();

  [SetUp]
  public void SetUp()
  {
    _policy = new HandledOnPaperPolicy();
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
    Assert.That(_policy.CanHandleOnPaper(PrintJobStatus.Failed, HealthyStation()), Is.True);
  }

  [Test]
  public void CanAcknowledge_AnUnknownTicketAtAHealthyStation_IsAllowed()
  {
    Assert.That(_policy.CanHandleOnPaper(PrintJobStatus.Unknown, HealthyStation()), Is.True);
  }

  [Test]
  public void CanAcknowledge_ABlockedTicketAtAHealthyStation_IsAllowed()
  {
    Assert.That(_policy.CanHandleOnPaper(PrintJobStatus.Blocked, HealthyStation()), Is.True);
  }

  [Test]
  public void CanAcknowledge_AFailedUnknownOrBlockedTicket_IsAllowedUnderEveryStationCondition()
  {
    PrintJobStatus[] alwaysAllowed =
    [
        PrintJobStatus.Failed,
            PrintJobStatus.Unknown,
            PrintJobStatus.Blocked,
        ];

    foreach (PrintJobStatus ticketStatus in alwaysAllowed)
    {
      Assert.That(_policy.CanHandleOnPaper(ticketStatus, HealthyStation()), Is.True, $"{ticketStatus}");

      foreach (StationPrintability station in StationsThatCannotPrint())
      {
        Assert.That(_policy.CanHandleOnPaper(ticketStatus, station), Is.True, $"{ticketStatus}");
      }
    }
  }

  [Test]
  public void CanAcknowledge_AQueuedTicketAtAFaultyStation_IsAllowed()
  {
    Assert.That(
        _policy.CanHandleOnPaper(PrintJobStatus.Queued, HealthyStation() with { IsFaulty = true }),
        Is.True);
  }

  [Test]
  public void CanAcknowledge_AQueuedTicketAtAnOfflineStation_IsAllowed()
  {
    Assert.That(
        _policy.CanHandleOnPaper(PrintJobStatus.Queued, HealthyStation() with { IsOnline = false }),
        Is.True);
  }

  [Test]
  public void CanAcknowledge_AQueuedTicketAtAStationOutOfPaper_IsAllowed()
  {
    Assert.That(
        _policy.CanHandleOnPaper(PrintJobStatus.Queued, HealthyStation() with { IsPaperEnd = true }),
        Is.True);
  }

  [Test]
  public void CanAcknowledge_AQueuedTicketAtAStationWithAnOpenCover_IsAllowed()
  {
    Assert.That(
        _policy.CanHandleOnPaper(PrintJobStatus.Queued, HealthyStation() with { IsCoverOpen = true }),
        Is.True);
  }

  [Test]
  public void CanAcknowledge_AQueuedTicketAtAStationInAnErrorState_IsAllowed()
  {
    Assert.That(
        _policy.CanHandleOnPaper(PrintJobStatus.Queued, HealthyStation() with { IsInErrorState = true }),
        Is.True);
  }

  [Test]
  public void CanAcknowledge_AQueuedTicketAtADisabledStation_IsAllowed()
  {
    Assert.That(
        _policy.CanHandleOnPaper(PrintJobStatus.Queued, HealthyStation() with { IsEnabled = false }),
        Is.True);
  }

  [Test]
  public void CanAcknowledge_AQueuedTicketAtAHealthyStation_IsRefused()
  {
    Assert.That(_policy.CanHandleOnPaper(PrintJobStatus.Queued, HealthyStation()), Is.False);
  }

  [Test]
  public void CanAcknowledge_APrintingTicketAtAHealthyStation_IsRefused()
  {
    Assert.That(_policy.CanHandleOnPaper(PrintJobStatus.Sending, HealthyStation()), Is.False);
  }

  [Test]
  public void CanAcknowledge_APrintingTicket_IsRefusedUnderEveryStationCondition()
  {
    foreach (StationPrintability station in StationsThatCannotPrint())
    {
      Assert.That(_policy.CanHandleOnPaper(PrintJobStatus.Sending, station), Is.False);
    }
  }

  [Test]
  public void CanAcknowledge_AFinishedTicketAtAHealthyStation_IsRefused()
  {
    Assert.Multiple(() =>
    {
      Assert.That(_policy.CanHandleOnPaper(PrintJobStatus.Printed, HealthyStation()), Is.False);
      Assert.That(_policy.CanHandleOnPaper(PrintJobStatus.HandledOnPaper, HealthyStation()), Is.False);
    });
  }
}
