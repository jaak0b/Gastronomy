using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;

namespace GastronomyApp.Infrastructure.Projections;

public sealed class FestivalProjection : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<FestivalContentCountsRow, FestivalContentCounts>().Map(counts => counts.FestivalId, row => row.Festival.Id);
  }
}
