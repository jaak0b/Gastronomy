namespace GastronomyApp.Core.Results;

public enum SettlementFailureReason
{
  PaymentNoticeMissing,
  UnknownOrderItemId,
  SelectionSpansSeveralTables,
  NoRunningFestival,
  DuplicateOrderItemId
}
