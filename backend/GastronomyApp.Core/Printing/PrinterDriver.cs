using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Printing;

public interface IPrinterDriver
{
    public Type PrinterType { get; }

    public int CharactersPerLine { get; }

    public string CodePageName { get; }

    public TimeSpan ConnectTimeout { get; }

    public TimeSpan JobTimeout { get; }

    public TimeSpan HeartbeatInterval { get; }

    public TimeSpan StatusQueryTimeout { get; }

    public Task<IPrinterSession> ConnectAsync(Printer printer, CancellationToken cancellationToken);
}

public abstract class PrinterDriver<TPrinter> : IPrinterDriver
    where TPrinter : Printer
{
    public Type PrinterType => typeof(TPrinter);

    public abstract int CharactersPerLine { get; }

    public abstract string CodePageName { get; }

    public abstract TimeSpan ConnectTimeout { get; }

    public abstract TimeSpan JobTimeout { get; }

    public abstract TimeSpan HeartbeatInterval { get; }

    public abstract TimeSpan StatusQueryTimeout { get; }

    public Task<IPrinterSession> ConnectAsync(Printer printer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(printer);

        if (printer is not TPrinter typed)
        {
            throw new PrinterDriverMismatchException(
                $"The driver for {typeof(TPrinter).Name} was handed a {printer.GetType().Name}, which it cannot talk to.");
        }

        return ConnectAsync(typed, cancellationToken);
    }

    protected abstract Task<IPrinterSession> ConnectAsync(TPrinter printer, CancellationToken cancellationToken);
}

public sealed class PrinterDriverMismatchException : Exception
{
    public PrinterDriverMismatchException()
    {
    }

    public PrinterDriverMismatchException(string message)
        : base(message)
    {
    }

    public PrinterDriverMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
