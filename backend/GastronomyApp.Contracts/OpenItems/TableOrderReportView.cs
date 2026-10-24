namespace GastronomyApp.Contracts.OpenItems;

public sealed record TableOrderReportView(Guid TableId, string TableName, int OpenAmountCents, IReadOnlyList<TableOrderRecordView> Orders);
