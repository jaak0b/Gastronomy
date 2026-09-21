namespace GastronomyApp.Contracts.OpenItems;

public sealed record TableOrderReportView(string TableName, int OpenAmountCents, IReadOnlyList<TableOrderRecordView> Orders);
