using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed record EventSessionStartRequest
{
    public required string Name { get; init; }
    public required bool IsPractice { get; init; }
    public string? TypedNameConfirmation { get; init; }
}

public sealed class EventSessionStartService
{
    public static readonly TimeSpan RecentOrderWindow = TimeSpan.FromHours(1);

    private readonly IEventSessionRepository _sessionRepository;
    private readonly IEventSessionStartGuardReader _guardReader;
    private readonly IClock _clock;

    public EventSessionStartService(
        IEventSessionRepository sessionRepository,
        IEventSessionStartGuardReader guardReader,
        IClock clock)
    {
        _sessionRepository = sessionRepository;
        _guardReader = guardReader;
        _clock = clock;
    }

    public async Task<Result<EventSessionStartDecision, EventSessionStartRefusal>> StartAsync(
        EventSessionStartRequest request,
        CancellationToken cancellationToken)
    {
        DateTime now = _clock.UtcNow;
        EventSession? activeSession = await _sessionRepository.FindActiveAsync(cancellationToken);

        List<EventSessionStartGuard> violatedGuards = [];
        int nonFinalTicketCount = 0;
        int unansweredUnknownCount = 0;

        if (activeSession is not null)
        {
            IReadOnlyCollection<LocationTicketStatus> ticketStatuses =
                await _guardReader.FindTicketStatusesAsync(activeSession.Id, cancellationToken);

            nonFinalTicketCount = ticketStatuses.Count(status => !IsFinal(status));
            unansweredUnknownCount = ticketStatuses.Count(status => status == LocationTicketStatus.Unknown);

            if (nonFinalTicketCount > 0)
            {
                violatedGuards.Add(EventSessionStartGuard.NonFinalTicketsRemain);
            }

            if (unansweredUnknownCount > 0)
            {
                violatedGuards.Add(EventSessionStartGuard.UnansweredUnknownQuestionsRemain);
            }

            DateTime? mostRecentOrderAcceptedAtUtc =
                await _guardReader.FindMostRecentOrderAcceptedAtUtcAsync(activeSession.Id, cancellationToken);

            if (NeedsTypedConfirmation(mostRecentOrderAcceptedAtUtc, now, request))
            {
                violatedGuards.Add(EventSessionStartGuard.RecentOrderNeedsTypedConfirmation);
            }
        }

        IReadOnlyCollection<ProductionLocation> locationsOnTestPrinter =
            await _guardReader.FindActiveLocationsOnTestPrinterAsync(cancellationToken);

        if (!request.IsPractice && locationsOnTestPrinter.Count > 0)
        {
            violatedGuards.Add(EventSessionStartGuard.ActiveLocationOnTestPrinter);
        }

        if (violatedGuards.Count > 0)
        {
            return Result<EventSessionStartDecision, EventSessionStartRefusal>.Failed(
                new EventSessionStartRefusal
                {
                    ViolatedGuards = violatedGuards,
                    NonFinalTicketCount = nonFinalTicketCount,
                    UnansweredUnknownCount = unansweredUnknownCount,
                    LocationsOnTestPrinter = locationsOnTestPrinter,
                });
        }

        return Result<EventSessionStartDecision, EventSessionStartRefusal>.Success(new EventSessionStartDecision
        {
            SessionToStart = new EventSession
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                IsPractice = request.IsPractice,
                StartedAtUtc = now,
                EndedAtUtc = null,
                IsActive = true,
            },
            PreviousSessionToEnd = EndedPreviousSession(activeSession, now),
        });
    }

    private EventSession? EndedPreviousSession(EventSession? activeSession, DateTime endedAtUtc)
    {
        if (activeSession is null)
        {
            return null;
        }

        activeSession.EndedAtUtc = endedAtUtc;
        activeSession.IsActive = false;

        return activeSession;
    }

    private bool NeedsTypedConfirmation(
        DateTime? mostRecentOrderAcceptedAtUtc,
        DateTime now,
        EventSessionStartRequest request)
    {
        if (mostRecentOrderAcceptedAtUtc is null)
        {
            return false;
        }

        if (now - mostRecentOrderAcceptedAtUtc.Value > RecentOrderWindow)
        {
            return false;
        }

        return !string.Equals(request.TypedNameConfirmation?.Trim(), request.Name.Trim(), StringComparison.Ordinal);
    }

    private bool IsFinal(LocationTicketStatus status)
    {
        return status switch
        {
            LocationTicketStatus.Printed => true,
            LocationTicketStatus.PrintedOnTestPrinter => true,
            LocationTicketStatus.HandledOnPaper => true,
            LocationTicketStatus.Queued => false,
            LocationTicketStatus.Blocked => false,
            LocationTicketStatus.Printing => false,
            LocationTicketStatus.Unknown => false,
            LocationTicketStatus.Failed => false,
            _ => new Never().OfType<bool>(status),
        };
    }
}
