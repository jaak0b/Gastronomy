using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminStationHandler
{
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly StationAdministrationService _service;

  public AdminStationHandler(StationAdministrationService service, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    return await _service.ListAsync(festivalId, cancellationToken).Match(stations => Results.Ok(new AdminStationListView(_mapper.Map<IReadOnlyList<AdminStationView>>(stations))), _resultEnvelope.Refuse);
  }

  public async Task<IResult> CreateAsync(SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.CreateAsync(request.Name, request.SortOrder, cancellationToken).Match(station => Results.Json(_mapper.Map<AdminStationView>(station), statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
  }

  public async Task<IResult> UpdateAsync(Guid stationId, SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.UpdateAsync(stationId, request.Name, request.SortOrder, cancellationToken).Match(station => Results.Ok(new SavedStationView(station.Id)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(stationId, cancellationToken).Match(station => Results.Ok(new SavedStationView(station.Id)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(stationId, cancellationToken).Match(station => Results.Ok(new SavedStationView(station.Id)), _resultEnvelope.Refuse);
  }
}
