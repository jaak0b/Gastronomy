namespace GastronomyApp.Core.Results;

public enum SettlementFailureReason
{
  NoItemsSelected,
  PaymentNoticeMissing,
  UnknownOrderItemId,
  AmountPaidMissing,
  AmountPaidNegative,
  SelectionSpansSeveralTables,
  NoRunningFestival,
  DuplicateOrderItemId
}
