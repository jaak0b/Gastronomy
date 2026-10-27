namespace GastronomyApp.Core.Results;

public enum OrderItemChangeVerdict
{
  Allowed,
  AlreadyInThatState,
  ItemIsCancelled,
  ItemIsNotHandedOut,
  ItemDoesNotAwaitCancellationAcknowledgement,
  ItemIsNotPlacedYet
}
