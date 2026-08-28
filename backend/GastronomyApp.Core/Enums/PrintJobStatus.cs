namespace GastronomyApp.Core.Enums;

public enum PrintJobStatus
{
  Queued = 0,
  Sending = 1,
  Printed = 2,
  Blocked = 3,
  Failed = 4,
  Unknown = 5,
  HandledOnPaper = 6,
}
