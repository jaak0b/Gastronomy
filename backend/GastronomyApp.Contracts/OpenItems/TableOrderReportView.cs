namespace GastronomyApp.Contracts.OpenItems;

public sealed record TableOrderReportView(Guid TableId, string TableName, IReadOnlyList<TableOrderRecordView> Orders);
