using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class EventSessionStartServiceTest
{
    private readonly Guid _activeSessionId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

    private IEventSessionRepository _sessionRepository = null!;
    private IEventSessionStartGuardReader _guardReader = null!;
    private IClock _clock = null!;
    private EventSessionStartService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _sessionRepository = A.Fake<IEventSessionRepository>();
        _guardReader = A.Fake<IEventSessionStartGuardReader>();
        _clock = A.Fake<IClock>();

        A.CallTo(() => _clock.UtcNow).Returns(_now);
        A.CallTo(() => _sessionRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<EventSession?>(ActiveSession()));
        A.CallTo(() => _guardReader.FindTicketStatusesAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<LocationTicketStatus>>([]));
        A.CallTo(() => _guardReader.FindMostRecentOrderAcceptedAtUtcAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult<DateTime?>(null));
        A.CallTo(() => _guardReader.FindActiveLocationsOnTestPrinterAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<ProductionLocation>>([]));

        _service = new EventSessionStartService(_sessionRepository, _guardReader, _clock);
    }

    private EventSession ActiveSession()
    {
        return new EventSession
        {
            Id = _activeSessionId,
            Name = "Freitagabend",
            IsPractice = false,
            StartedAtUtc = _now.AddHours(-6),
            EndedAtUtc = null,
            IsActive = true,
        };
    }

    private ProductionLocation KitchenOnTestPrinter()
    {
        return new ProductionLocation
        {
            Id = _kitchenId,
            Name = "Kueche",
            StationAccessKey = "kitchen-key",
            SlipLanguage = "de",
            SortOrder = 1,
            IsActive = true,
        };
    }

    private EventSessionStartRequest RequestWith(
        string name = "Samstagabend",
        bool isPractice = false,
        string? typedNameConfirmation = null)
    {
        return new EventSessionStartRequest
        {
            Name = name,
            IsPractice = isPractice,
            TypedNameConfirmation = typedNameConfirmation,
        };
    }

    private void GivenTicketStatuses(params LocationTicketStatus[] statuses)
    {
        A.CallTo(() => _guardReader.FindTicketStatusesAsync(_activeSessionId, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<LocationTicketStatus>>(statuses));
    }

    private void GivenMostRecentOrderAt(DateTime? acceptedAtUtc)
    {
        A.CallTo(() => _guardReader.FindMostRecentOrderAcceptedAtUtcAsync(_activeSessionId, A<CancellationToken>._))
            .Returns(Task.FromResult(acceptedAtUtc));
    }

    private void GivenALocationOnTheTestPrinter()
    {
        A.CallTo(() => _guardReader.FindActiveLocationsOnTestPrinterAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyCollection<ProductionLocation>>([KitchenOnTestPrinter()]));
    }

    [Test]
    public async Task StartAsync_NothingHoldingTheCurrentSession_StartsTheNewSession()
    {
        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.SessionToStart.Name, Is.EqualTo("Samstagabend"));
            Assert.That(result.Value.SessionToStart.IsActive, Is.True);
            Assert.That(result.Value.SessionToStart.IsPractice, Is.False);
            Assert.That(result.Value.SessionToStart.StartedAtUtc, Is.EqualTo(_now));
            Assert.That(result.Value.SessionToStart.EndedAtUtc, Is.Null);
            Assert.That(result.Value.SessionToStart.Id, Is.Not.EqualTo(Guid.Empty));
        });
    }

    [Test]
    public async Task StartAsync_AnActiveSessionExists_EndsItInTheSameDecision()
    {
        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        EventSession? previous = result.Value.PreviousSessionToEnd;

        Assert.Multiple(() =>
        {
            Assert.That(previous, Is.Not.Null);
            Assert.That(previous!.Id, Is.EqualTo(_activeSessionId));
            Assert.That(previous.IsActive, Is.False);
            Assert.That(previous.EndedAtUtc, Is.EqualTo(_now));
        });
    }

    [Test]
    public async Task StartAsync_NoSessionHasEverBeenStarted_StartsWithNothingToEnd()
    {
        A.CallTo(() => _sessionRepository.FindActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult<EventSession?>(null));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.PreviousSessionToEnd, Is.Null);
        });
    }

    [Test]
    public async Task StartAsync_ATicketOfTheCurrentSessionIsNotFinal_RefusesNamingHowMany()
    {
        GivenTicketStatuses(
            LocationTicketStatus.Queued,
            LocationTicketStatus.Printed,
            LocationTicketStatus.Blocked);

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Failure.ViolatedGuards,
                Does.Contain(EventSessionStartGuard.NonFinalTicketsRemain));
            Assert.That(result.Failure.NonFinalTicketCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task StartAsync_EveryTicketOfTheCurrentSessionIsFinal_DoesNotRaiseThatGuard()
    {
        GivenTicketStatuses(
            LocationTicketStatus.Printed,
            LocationTicketStatus.PrintedOnTestPrinter,
            LocationTicketStatus.HandledOnPaper);

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task StartAsync_AnUnknownQuestionIsUnanswered_RefusesNamingHowMany()
    {
        GivenTicketStatuses(LocationTicketStatus.Unknown, LocationTicketStatus.Printed);

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Failure.ViolatedGuards,
                Does.Contain(EventSessionStartGuard.UnansweredUnknownQuestionsRemain));
            Assert.That(result.Failure.UnansweredUnknownCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task StartAsync_AnUnknownQuestionIsUnanswered_AlsoCountsAsANonFinalTicket()
    {
        GivenTicketStatuses(LocationTicketStatus.Unknown);

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.That(
            result.Failure.ViolatedGuards,
            Is.EquivalentTo(new[]
            {
                EventSessionStartGuard.NonFinalTicketsRemain,
                EventSessionStartGuard.UnansweredUnknownQuestionsRemain,
            }));
    }

    [Test]
    public async Task StartAsync_AnOrderWasAcceptedWithinTheLastHourAndNothingWasTyped_Refuses()
    {
        GivenMostRecentOrderAt(_now.AddMinutes(-59));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Failure.ViolatedGuards,
                Does.Contain(EventSessionStartGuard.RecentOrderNeedsTypedConfirmation));
        });
    }

    [Test]
    public async Task StartAsync_AnOrderWasAcceptedWithinTheLastHourAndTheNameWasTyped_Starts()
    {
        GivenMostRecentOrderAt(_now.AddMinutes(-59));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result = await _service.StartAsync(
            RequestWith(typedNameConfirmation: "Samstagabend"),
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task StartAsync_AnOrderWasAcceptedWithinTheLastHourAndTheWrongNameWasTyped_Refuses()
    {
        GivenMostRecentOrderAt(_now.AddMinutes(-59));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result = await _service.StartAsync(
            RequestWith(typedNameConfirmation: "Freitagabend"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Failure.ViolatedGuards,
                Does.Contain(EventSessionStartGuard.RecentOrderNeedsTypedConfirmation));
        });
    }

    [Test]
    public async Task StartAsync_TheTypedNameCarriesSurroundingWhitespace_StillStarts()
    {
        GivenMostRecentOrderAt(_now.AddMinutes(-59));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result = await _service.StartAsync(
            RequestWith(typedNameConfirmation: "  Samstagabend "),
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task StartAsync_TheEventNameItselfCarriesSurroundingWhitespace_StillStarts()
    {
        GivenMostRecentOrderAt(_now.AddMinutes(-59));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result = await _service.StartAsync(
            RequestWith(name: "Samstagabend ", typedNameConfirmation: "Samstagabend"),
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task StartAsync_TheTypedNameDiffersOnlyInCase_StillRefuses()
    {
        GivenMostRecentOrderAt(_now.AddMinutes(-59));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result = await _service.StartAsync(
            RequestWith(typedNameConfirmation: "samstagabend"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Failure.ViolatedGuards,
                Does.Contain(EventSessionStartGuard.RecentOrderNeedsTypedConfirmation));
        });
    }

    [Test]
    public async Task StartAsync_TheTypedNameIsWhitespaceOnlyAgainstARealName_StillRefuses()
    {
        GivenMostRecentOrderAt(_now.AddMinutes(-59));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result = await _service.StartAsync(
            RequestWith(typedNameConfirmation: "   "),
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public async Task StartAsync_TheLastOrderWasAcceptedMoreThanAnHourAgo_NeedsNoTypedConfirmation()
    {
        GivenMostRecentOrderAt(_now.AddMinutes(-61));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task StartAsync_TheLastOrderWasAcceptedExactlyAnHourAgo_StillNeedsTypedConfirmation()
    {
        GivenMostRecentOrderAt(_now.AddHours(-1));

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Failure.ViolatedGuards,
                Does.Contain(EventSessionStartGuard.RecentOrderNeedsTypedConfirmation));
        });
    }

    [Test]
    public async Task StartAsync_AnActiveLocationIsStillOnTheTestPrinter_RefusesNamingTheStations()
    {
        GivenALocationOnTheTestPrinter();

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Failure.ViolatedGuards,
                Does.Contain(EventSessionStartGuard.ActiveLocationOnTestPrinter));
            Assert.That(
                result.Failure.LocationsOnTestPrinter.Select(location => location.Id),
                Is.EquivalentTo(new[] { _kitchenId }));
        });
    }

    [Test]
    public async Task StartAsync_APracticeRunWithAStationOnTheTestPrinter_IsExemptFromThatGuard()
    {
        GivenALocationOnTheTestPrinter();

        Result<EventSessionStartDecision, EventSessionStartRefusal> result = await _service.StartAsync(
            RequestWith(isPractice: true),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.SessionToStart.IsPractice, Is.True);
        });
    }

    [Test]
    public async Task StartAsync_EveryGuardViolatedAtOnce_ReportsAllOfThemRatherThanTheFirst()
    {
        GivenTicketStatuses(LocationTicketStatus.Queued, LocationTicketStatus.Unknown);
        GivenMostRecentOrderAt(_now.AddMinutes(-5));
        GivenALocationOnTheTestPrinter();

        Result<EventSessionStartDecision, EventSessionStartRefusal> result =
            await _service.StartAsync(RequestWith(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Failure.ViolatedGuards,
                Is.EquivalentTo(Enum.GetValues<EventSessionStartGuard>()));
            Assert.That(result.Failure.NonFinalTicketCount, Is.EqualTo(2));
            Assert.That(result.Failure.UnansweredUnknownCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task StartAsync_EveryDeclaredGuard_IsRaisedBySomeScenario()
    {
        foreach (EventSessionStartGuard guard in Enum.GetValues<EventSessionStartGuard>())
        {
            SetUp();

            switch (guard)
            {
                case EventSessionStartGuard.NonFinalTicketsRemain:
                    GivenTicketStatuses(LocationTicketStatus.Queued);
                    break;
                case EventSessionStartGuard.UnansweredUnknownQuestionsRemain:
                    GivenTicketStatuses(LocationTicketStatus.Unknown);
                    break;
                case EventSessionStartGuard.RecentOrderNeedsTypedConfirmation:
                    GivenMostRecentOrderAt(_now.AddMinutes(-5));
                    break;
                case EventSessionStartGuard.ActiveLocationOnTestPrinter:
                    GivenALocationOnTheTestPrinter();
                    break;
                default:
                    throw new InvalidOperationException($"No scenario covers {guard}");
            }

            Result<EventSessionStartDecision, EventSessionStartRefusal> result =
                await _service.StartAsync(RequestWith(), CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False, $"{guard}");
            Assert.That(result.Failure.ViolatedGuards, Does.Contain(guard), $"{guard}");
        }
    }

    [Test]
    public async Task StartAsync_EveryDeclaredTicketStatus_IsCountedAsFinalOrNotExactlyOnce()
    {
        LocationTicketStatus[] finalStatuses =
        [
            LocationTicketStatus.Printed,
            LocationTicketStatus.PrintedOnTestPrinter,
            LocationTicketStatus.HandledOnPaper,
        ];

        foreach (LocationTicketStatus status in Enum.GetValues<LocationTicketStatus>())
        {
            SetUp();
            GivenTicketStatuses(status);

            Result<EventSessionStartDecision, EventSessionStartRefusal> result =
                await _service.StartAsync(RequestWith(), CancellationToken.None);

            bool isFinal = finalStatuses.Contains(status);

            if (isFinal)
            {
                Assert.That(result.IsSuccess, Is.True, $"{status} is a final state");
            }
            else
            {
                Assert.That(result.IsSuccess, Is.False, $"{status} is not a final state");
                Assert.That(result.Failure.NonFinalTicketCount, Is.EqualTo(1), $"{status}");
            }
        }
    }
}
