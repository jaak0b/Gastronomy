namespace GastronomyApp.Core.Enums;

public enum PrintFailureReason
{
  PaperEnd = 0,
  CoverOpen = 1,
  Unreachable = 2,
  Timeout = 3,
  SocketDropped = 4,
  PrinterError = 5,
  StationDisabled = 6,
  StationFaulty = 7,
  HandledOnPaper = 8
}
