using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminStationHandler
{
  private readonly IMapper _mapper;
  private readonly StationAdministrationService _service;

  public AdminStationHandler(StationAdministrationService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<AdminStationListView>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    return await _service.ListAsync(festivalId, cancellationToken).Then(stations => new AdminStationListView(_mapper.Map<IReadOnlyList<AdminStationView>>(stations)));
  }

  public async Task<CreatedAnswer<AdminStationView>> CreateAsync(SaveStationRequest request, CancellationToken cancellationToken)
  {
    return await _service.CreateAsync(request.Name, request.SortOrder, cancellationToken).Then(_mapper.Map<AdminStationView>);
  }

  public async Task<ApiAnswer<SavedStationView>> UpdateAsync(Guid stationId, SaveStationRequest request, CancellationToken cancellationToken)
  {
    return await _service.UpdateAsync(stationId, request.Name, request.SortOrder, cancellationToken).Then(station => new SavedStationView(station.Id));
  }

  public async Task<ApiAnswer<SavedStationView>> ActivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(stationId, cancellationToken).Then(station => new SavedStationView(station.Id));
  }

  public async Task<ApiAnswer<SavedStationView>> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(stationId, cancellationToken).Then(station => new SavedStationView(station.Id));
  }
}
