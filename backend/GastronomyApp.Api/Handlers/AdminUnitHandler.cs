using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Units;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminUnitHandler
{
  private readonly IMapper _mapper;
  private readonly UnitAdministrationService _service;

  public AdminUnitHandler(UnitAdministrationService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<AdminUnitListView>> ListAsync(CancellationToken cancellationToken)
  {
    return new AdminUnitListView(_mapper.Map<IReadOnlyList<AdminUnitView>>(await _service.ListAsync(cancellationToken)));
  }

  public async Task<CreatedAnswer<AdminUnitView>> CreateAsync(SaveUnitRequest request, CancellationToken cancellationToken)
  {
    return await _service.CreateAsync(request.Name, cancellationToken).Then(_mapper.Map<AdminUnitView>);
  }

  public async Task<ApiAnswer<SavedUnitView>> UpdateAsync(Guid unitId, SaveUnitRequest request, CancellationToken cancellationToken)
  {
    return await _service.UpdateAsync(unitId, request.Name, cancellationToken).Then(unit => new SavedUnitView(unit.Id));
  }

  public async Task<ApiAnswer<SavedUnitView>> ActivateAsync(Guid unitId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(unitId, cancellationToken).Then(unit => new SavedUnitView(unit.Id));
  }

  public async Task<ApiAnswer<SavedUnitView>> DeactivateAsync(Guid unitId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(unitId, cancellationToken).Then(unit => new SavedUnitView(unit.Id));
  }
}
