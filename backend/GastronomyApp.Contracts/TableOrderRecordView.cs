namespace GastronomyApp.Contracts;

public sealed record TableOrderRecordView(Guid OrderId, int GlobalOrderNumber, DateTime CreatedAtUtc, string StaffMemberName, IReadOnlyList<TableOrderRecordItemView> Items);
