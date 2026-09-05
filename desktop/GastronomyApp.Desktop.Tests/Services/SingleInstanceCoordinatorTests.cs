using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.Services.Windows;
using GastronomyApp.Desktop.Tests.Logging;
using Serilog.Events;

namespace GastronomyApp.Desktop.Tests.Services;

[TestFixture]
public sealed class SingleInstanceCoordinatorTests
{

  private static string UnusedName()
  {
    return $"GastronomyApp.Desktop.Tests.{Guid.NewGuid():N}";
  }

  [Test]
  public void AcquireOrSignalExisting_WhenNobodyElseHoldsTheInstance_TakesIt()
  {
    using SingleInstanceCoordinator coordinator = new(UnusedName(), UnusedName());

    var outcome = coordinator.AcquireOrSignalExisting();

    Assert.That(outcome, Is.EqualTo(SingleInstanceOutcome.AcquiredPrimary));
  }

  [Test]
  public void AcquireOrSignalExisting_WhenTheRunningInstanceDoesNotAnswer_WritesTheFailureToTheLogWithTheReason()
  {
    using RecordedLog log = new();
    var instanceName = UnusedName();
    using Mutex held = new(true, instanceName);
    using SingleInstanceCoordinator coordinator = new(instanceName, UnusedName());

    coordinator.AcquireOrSignalExisting();

    Assert.That(log.Entries.Where(entry => entry.Level == LogEventLevel.Error
                                           && entry.Exception is not null),
                Is.Not.Empty);
  }

  [Test]
  public void AcquireOrSignalExisting_WhenTheRunningInstanceDoesNotAnswer_SaysInTheLogThatNoWindowCameToTheFront()
  {
    using RecordedLog log = new();
    var instanceName = UnusedName();
    using Mutex held = new(true, instanceName);
    using SingleInstanceCoordinator coordinator = new(instanceName, UnusedName());

    coordinator.AcquireOrSignalExisting();

    Assert.That(log.Entries.Select(entry => entry.RenderMessage()),
                Has.Some.Contains("front"));
  }

  [Test]
  public void AcquireOrSignalExisting_WhenTheRunningInstanceDoesNotAnswer_StillLeavesTheSecondStartExiting()
  {
    var instanceName = UnusedName();
    using Mutex held = new(true, instanceName);
    using SingleInstanceCoordinator coordinator = new(instanceName, UnusedName());

    var outcome = coordinator.AcquireOrSignalExisting();

    Assert.That(outcome, Is.EqualTo(SingleInstanceOutcome.SignaledExistingAndShouldExit));
  }
}
