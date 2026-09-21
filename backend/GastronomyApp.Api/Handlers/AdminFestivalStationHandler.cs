using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalStationHandler
{
  private readonly ResultEnvelope _resultEnvelope;
  private readonly FestivalStationService _service;

  public AdminFestivalStationHandler(FestivalStationService service, ResultEnvelope resultEnvelope)
  {
    _service = service;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> AddAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.AddAsync(festivalId, stationId, cancellationToken).Match(link => Results.Ok(new SavedStationView(link.StationId)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.RemoveAsync(festivalId, stationId, cancellationToken).Match(link => Results.NoContent(), _resultEnvelope.Refuse);
  }
}
