using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Printing;

public sealed class PrinterDriverRegistry
{
    private readonly Dictionary<Type, IPrinterDriver> driversByPrinterType;

    public PrinterDriverRegistry(IEnumerable<IPrinterDriver> drivers)
    {
        ArgumentNullException.ThrowIfNull(drivers);

        driversByPrinterType = [];
        foreach (IPrinterDriver driver in drivers)
        {
            if (!driversByPrinterType.TryAdd(driver.PrinterType, driver))
            {
                throw new DuplicatePrinterDriverException(
                    $"Two printer drivers claim {driver.PrinterType.Name}, so the app cannot tell which one to use.");
            }
        }
    }

    public IPrinterDriver For(Printer printer)
    {
        ArgumentNullException.ThrowIfNull(printer);

        if (driversByPrinterType.TryGetValue(printer.GetType(), out IPrinterDriver? driver))
        {
            return driver;
        }

        throw new UnknownPrinterDriverException(
            $"This build has no driver for the printer {printer.Name}, so it cannot be used.");
    }
}

public sealed class DuplicatePrinterDriverException : Exception
{
    public DuplicatePrinterDriverException()
    {
    }

    public DuplicatePrinterDriverException(string message)
        : base(message)
    {
    }

    public DuplicatePrinterDriverException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class UnknownPrinterDriverException : Exception
{
    public UnknownPrinterDriverException()
    {
    }

    public UnknownPrinterDriverException(string message)
        : base(message)
    {
    }

    public UnknownPrinterDriverException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
