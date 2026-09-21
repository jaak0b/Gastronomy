using ErrorOr;
using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalStationHandler
{
  private readonly StationChangeAnnouncer _announcer;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly FestivalStationService _service;

  public AdminFestivalStationHandler(FestivalStationService service, StationChangeAnnouncer announcer, ResultEnvelope resultEnvelope)
  {
    _service = service;
    _announcer = announcer;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> AddAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.AddAsync(festivalId, stationId, cancellationToken)
                         .ThenDoAsync(link => _announcer.AnnounceAsync(link.StationId))
                         .Match(link => Results.Ok(new SavedStationView(link.StationId)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.RemoveAsync(festivalId, stationId, cancellationToken)
                         .ThenDoAsync(link => _announcer.AnnounceAsync(link.StationId))
                         .Match(link => Results.NoContent(), _resultEnvelope.Refuse);
  }
}
