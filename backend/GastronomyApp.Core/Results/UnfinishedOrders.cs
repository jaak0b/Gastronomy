using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Core.Results;

public sealed record UnfinishedOrders(int Count, int PartlyDoneCount)
{
  public ShareState Progress
  {
    get
    {
      if (Count == 0)
        return ShareState.All;

      if (PartlyDoneCount > 0)
        return ShareState.Some;

      return ShareState.None;
    }
  }
}
