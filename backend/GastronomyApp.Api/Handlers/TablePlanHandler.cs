using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.TablePlan;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class TablePlanHandler
{
  private readonly IMapper _mapper;
  private readonly TablePlanService _service;

  public TablePlanHandler(TablePlanService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<TablePlanView>> ReadAsync(CancellationToken cancellationToken)
  {
    return new TablePlanView(_mapper.Map<IReadOnlyList<TablePlanAreaView>>(await _service.ReadRunningFestivalTablePlanAsync(cancellationToken)));
  }
}
