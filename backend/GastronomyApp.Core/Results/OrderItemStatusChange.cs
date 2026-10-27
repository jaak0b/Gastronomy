namespace GastronomyApp.Core.Results;

public enum OrderItemStatusChange
{
  Allowed = 0,
  AlreadyInThatState = 1,
  ItemIsCancelled = 2,
  NotAllowed = 3
}
