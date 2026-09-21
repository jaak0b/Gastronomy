using GastronomyApp.Contracts;
using GastronomyApp.Core.ReadModels;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class OpenItemMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<OpenOrderItem, OpenOrderItemView>();

    config.NewConfig<OpenTable, OpenTableView>();

    config.NewConfig<TableOrderRecordItem, TableOrderRecordItemView>();

    config.NewConfig<TableOrderRecord, TableOrderRecordView>();

    config.NewConfig<TableOrderReport, TableOrderReportView>();
  }
}
