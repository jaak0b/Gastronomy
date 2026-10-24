using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalStationShapeHandler
{
  private readonly FestivalStationShapeService _service;

  public AdminFestivalStationShapeHandler(FestivalStationShapeService service)
  {
    _service = service;
  }

  public async Task<ApiAnswer<SavedStationView>> PlaceAsync(Guid festivalId, Guid stationId, SaveStationShapeRequest request, CancellationToken cancellationToken)
  {
    return await _service.PlaceAsync(festivalId, stationId, request, cancellationToken).Then(shape => new SavedStationView(stationId));
  }

  public async Task<NoContentAnswer> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.RemoveAsync(festivalId, stationId, cancellationToken).Then(shape => Result.Success);
  }
}
