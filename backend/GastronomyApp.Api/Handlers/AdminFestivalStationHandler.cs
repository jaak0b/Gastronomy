using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalStationHandler
{
  private readonly FestivalStationService _service;

  public AdminFestivalStationHandler(FestivalStationService service)
  {
    _service = service;
  }

  public async Task<ApiAnswer<SavedStationView>> AddAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.AddAsync(festivalId, stationId, cancellationToken).Then(link => new SavedStationView(link.StationId));
  }

  public async Task<NoContentAnswer> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.RemoveAsync(festivalId, stationId, cancellationToken).Then(link => Result.Success);
  }
}
