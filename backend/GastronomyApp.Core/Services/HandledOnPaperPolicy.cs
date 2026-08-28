using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class HandledOnPaperPolicy
{
    public bool CanHandleOnPaper(PrintJobStatus printJobStatus, StationPrintability station)
    {
        if (printJobStatus is PrintJobStatus.Failed
            or PrintJobStatus.Unknown
            or PrintJobStatus.Blocked)
        {
            return true;
        }

        bool stationCannotPrintRightNow = station.IsFaulty
            || !station.IsOnline
            || station.IsPaperEnd
            || station.IsCoverOpen
            || station.IsInErrorState
            || !station.IsEnabled;

        return stationCannotPrintRightNow && printJobStatus is not PrintJobStatus.Sending;
    }
}
