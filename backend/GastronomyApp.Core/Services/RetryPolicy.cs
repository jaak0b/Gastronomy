using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class RetryPolicy
{
    public PrintOutcomeMapping Map(PrintAttemptOutcome outcome, TransportKind transportKind, int bytesWritten)
    {
        bool anyByteReachedThePrinter = bytesWritten > 0;

        return (outcome, anyByteReachedThePrinter, transportKind) switch
        {
            (PrintAttemptOutcome.Confirmed, true, TransportKind.Mock) => new PrintOutcomeMapping
            {
                JobStatus = PrintJobStatus.Confirmed,
                TicketStatus = LocationTicketStatus.PrintedOnTestPrinter,
                ShouldRetryAutomatically = false,
            },
            (PrintAttemptOutcome.Confirmed, true, TransportKind.Network) => ConfirmedOnARealPrinter(),
            (PrintAttemptOutcome.Confirmed, true, TransportKind.Agent) => ConfirmedOnARealPrinter(),
            (PrintAttemptOutcome.Blocked, false, _) => HeldByTheStation(),
            (PrintAttemptOutcome.PrinterError, false, _) => HeldByTheStation(),
            (PrintAttemptOutcome.Unreachable, false, _) => WaitingForAnotherAttempt(),
            (PrintAttemptOutcome.SocketDropped, false, _) => WaitingForAnotherAttempt(),
            (PrintAttemptOutcome.Timeout, false, _) => WaitingForAnotherAttempt(),
            (PrintAttemptOutcome.SocketDropped, true, _) => OutcomeIsNotKnowable(),
            (PrintAttemptOutcome.Timeout, true, _) => OutcomeIsNotKnowable(),
            (PrintAttemptOutcome.PrinterError, true, _) => OutcomeIsNotKnowable(),
            _ => new Never().OfType<PrintOutcomeMapping>(outcome),
        };
    }

    private PrintOutcomeMapping ConfirmedOnARealPrinter()
    {
        return new PrintOutcomeMapping
        {
            JobStatus = PrintJobStatus.Confirmed,
            TicketStatus = LocationTicketStatus.Printed,
            ShouldRetryAutomatically = false,
        };
    }

    private PrintOutcomeMapping HeldByTheStation()
    {
        return new PrintOutcomeMapping
        {
            JobStatus = PrintJobStatus.Blocked,
            TicketStatus = LocationTicketStatus.Blocked,
            ShouldRetryAutomatically = true,
        };
    }

    private PrintOutcomeMapping WaitingForAnotherAttempt()
    {
        return new PrintOutcomeMapping
        {
            JobStatus = PrintJobStatus.Queued,
            TicketStatus = LocationTicketStatus.Queued,
            ShouldRetryAutomatically = true,
        };
    }

    private PrintOutcomeMapping OutcomeIsNotKnowable()
    {
        return new PrintOutcomeMapping
        {
            JobStatus = PrintJobStatus.Unknown,
            TicketStatus = LocationTicketStatus.Unknown,
            ShouldRetryAutomatically = false,
        };
    }
}
