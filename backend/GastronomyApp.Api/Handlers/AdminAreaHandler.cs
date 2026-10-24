using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminAreaHandler
{
  private readonly IMapper _mapper;
  private readonly AreaAdministrationService _service;

  public AdminAreaHandler(AreaAdministrationService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<AdminTablePlanView>> ListTablePlanAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _service.ListTablePlanAsync(festivalId, cancellationToken).Then(areas => new AdminTablePlanView(_mapper.Map<IReadOnlyList<AdminAreaView>>(areas)));
  }

  public async Task<CreatedAnswer<SavedAreaView>> CreateAsync(Guid festivalId, SaveAreaRequest request, CancellationToken cancellationToken)
  {
    return await _service.CreateAsync(festivalId, request, cancellationToken).Then(area => new SavedAreaView(area.Id));
  }

  public async Task<ApiAnswer<SavedAreaView>> UpdateAsync(Guid festivalId, Guid areaId, SaveAreaRequest request, CancellationToken cancellationToken)
  {
    return await _service.UpdateAsync(festivalId, areaId, request, cancellationToken).Then(area => new SavedAreaView(area.Id));
  }

  public async Task<ApiAnswer<SavedAreaView>> HideAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _service.HideAsync(festivalId, areaId, cancellationToken).Then(area => new SavedAreaView(area.Id));
  }

  public async Task<ApiAnswer<SavedAreaView>> ShowAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _service.ShowAsync(festivalId, areaId, cancellationToken).Then(area => new SavedAreaView(area.Id));
  }

  public async Task<NoContentAnswer> DeleteAsync(Guid festivalId, Guid areaId, CancellationToken cancellationToken)
  {
    return await _service.DeleteAsync(festivalId, areaId, cancellationToken).Then(area => Result.Success);
  }
}
