namespace GastronomyApp.Api.Contracts;

public sealed record TableOrderReportView(string TableName, int OpenAmountCents, IReadOnlyList<TableOrderRecordView> Orders);
