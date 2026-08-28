using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class RetryPolicy
{
    public PrintOutcomeMapping Map(PrintOutcome outcome, int bytesWritten)
    {
        bool anyByteReachedThePrinter = bytesWritten > 0;

        return (outcome, anyByteReachedThePrinter) switch
        {
            (PrintOutcome.Confirmed, true) => ConfirmedOnPaper(),
            (PrintOutcome.Blocked, false) => HeldByTheStation(),
            (PrintOutcome.PrinterError, false) => HeldByTheStation(),
            (PrintOutcome.Unreachable, false) => WaitingForAnotherAttempt(),
            (PrintOutcome.SocketDropped, false) => WaitingForAnotherAttempt(),
            (PrintOutcome.Timeout, false) => WaitingForAnotherAttempt(),
            (PrintOutcome.SocketDropped, true) => OutcomeIsNotKnowable(),
            (PrintOutcome.Timeout, true) => OutcomeIsNotKnowable(),
            (PrintOutcome.PrinterError, true) => OutcomeIsNotKnowable(),
            _ => new Never().OfType<PrintOutcomeMapping>(outcome),
        };
    }

    private PrintOutcomeMapping ConfirmedOnPaper()
    {
        return new PrintOutcomeMapping
        {
            JobStatus = PrintJobStatus.Printed,
            ShouldRetryAutomatically = false,
        };
    }

    private PrintOutcomeMapping HeldByTheStation()
    {
        return new PrintOutcomeMapping
        {
            JobStatus = PrintJobStatus.Blocked,
            ShouldRetryAutomatically = true,
        };
    }

    private PrintOutcomeMapping WaitingForAnotherAttempt()
    {
        return new PrintOutcomeMapping
        {
            JobStatus = PrintJobStatus.Queued,
            ShouldRetryAutomatically = true,
        };
    }

    private PrintOutcomeMapping OutcomeIsNotKnowable()
    {
        return new PrintOutcomeMapping
        {
            JobStatus = PrintJobStatus.Unknown,
            ShouldRetryAutomatically = false,
        };
    }
}
