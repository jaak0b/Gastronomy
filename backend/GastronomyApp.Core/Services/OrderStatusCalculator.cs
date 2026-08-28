using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed class OrderStatusCalculator
{
  public OrderStatus Calculate(IReadOnlyCollection<PrintJobStatus> printJobStatuses)
  {
    if (printJobStatuses.Any(NeedsHumanAttention))
    {
      return OrderStatus.NeedsAttention;
    }

    if (printJobStatuses.All(IsOnPaper))
    {
      return OrderStatus.Printed;
    }

    if (printJobStatuses.Any(status => status == PrintJobStatus.Sending))
    {
      return OrderStatus.Printing;
    }

    return OrderStatus.Accepted;
  }

  private bool NeedsHumanAttention(PrintJobStatus status)
  {
    return status switch
    {
      PrintJobStatus.Unknown => true,
      PrintJobStatus.Failed => true,
      PrintJobStatus.Blocked => true,
      PrintJobStatus.Queued => false,
      PrintJobStatus.Sending => false,
      PrintJobStatus.Printed => false,
      PrintJobStatus.HandledOnPaper => false,
      _ => new Never().OfType<bool>(status),
    };
  }

  private bool IsOnPaper(PrintJobStatus status)
  {
    return status switch
    {
      PrintJobStatus.Printed => true,
      PrintJobStatus.HandledOnPaper => true,
      PrintJobStatus.Queued => false,
      PrintJobStatus.Blocked => false,
      PrintJobStatus.Sending => false,
      PrintJobStatus.Unknown => false,
      PrintJobStatus.Failed => false,
      _ => new Never().OfType<bool>(status),
    };
  }
}
