namespace GastronomyApp.Contracts.OpenItems;

public sealed record TableOrderRecordView(Guid OrderId, int GlobalOrderNumber, DateTime CreatedAtUtc, string StaffMemberName, int NotCancelledItemCount, int HandedOutItemCount, IReadOnlyList<TableOrderRecordItemView> Items);
