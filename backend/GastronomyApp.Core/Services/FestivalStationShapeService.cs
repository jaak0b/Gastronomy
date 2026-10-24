using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class FestivalStationShapeService
{
  private readonly AreaShapeService _areaShapeService;
  private readonly IFestivalStationRepository _festivalStationRepository;
  private readonly ITablePlanRepository _tablePlanRepository;

  public FestivalStationShapeService(IFestivalStationRepository festivalStationRepository, ITablePlanRepository tablePlanRepository, AreaShapeService areaShapeService)
  {
    _festivalStationRepository = festivalStationRepository;
    _tablePlanRepository = tablePlanRepository;
    _areaShapeService = areaShapeService;
  }

  public async Task<ErrorOr<FestivalStationShape>> PlaceAsync(Guid festivalId, Guid stationId, SaveStationShapeRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var link = await _festivalStationRepository.FindLinkAsync(festivalId, stationId, cancellationToken);

    if (link is null)
      return Refusal.TablePlan.StationLinkNotFound(festivalId, stationId);

    if (await _tablePlanRepository.FindAreaAsync(festivalId, request.AreaId, cancellationToken) is null)
      return Refusal.TablePlan.AreaOutsideTheFestival(festivalId, request.AreaId);

    var shape = await _tablePlanRepository.FindStationShapeAsync(link.Id, cancellationToken);

    if (shape is null)
    {
      shape = new()
              {
                FestivalStationId = link.Id,
                FestivalId = festivalId,
                AreaId = request.AreaId
              };

      await _tablePlanRepository.AddStationShapeAsync(shape, cancellationToken);
    }
    else
      shape.AreaId = request.AreaId;

    _areaShapeService.Reshape(shape, request.Geometry!);
    await _tablePlanRepository.SaveChangesAsync(cancellationToken);

    return shape;
  }

  public async Task<ErrorOr<FestivalStationShape>> RemoveAsync(Guid festivalId, Guid stationId, CancellationToken cancellationToken)
  {
    var link = await _festivalStationRepository.FindLinkAsync(festivalId, stationId, cancellationToken);

    if (link is null)
      return Refusal.TablePlan.StationLinkNotFound(festivalId, stationId);

    var shape = await _tablePlanRepository.FindStationShapeAsync(link.Id, cancellationToken);

    if (shape is null)
      return Refusal.TablePlan.StationShapeNotFound(festivalId, stationId);

    _tablePlanRepository.RemoveStationShape(shape);
    await _tablePlanRepository.SaveChangesAsync(cancellationToken);

    return shape;
  }
}
