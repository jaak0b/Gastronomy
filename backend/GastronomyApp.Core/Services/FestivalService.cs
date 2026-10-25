using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class FestivalService
{
  private const int FirstNumber = 1;
  private readonly AreaShapeService _areaShapeService;
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly FestivalSchedule _schedule;

  public FestivalService(IGastronomyDatabase database, TypeAdapterConfig mapping, FestivalSchedule schedule, RunningFestivalLookup runningFestival, AreaShapeService areaShapeService)
  {
    _database = database;
    _mapping = mapping;
    _schedule = schedule;
    _runningFestival = runningFestival;
    _areaShapeService = areaShapeService;
  }

  public async Task<AdminFestivalListView> ListAsync(CancellationToken cancellationToken)
  {
    var festivals = await _database.Festivals.AsNoTracking().OrderByDescending(festival => festival.StartsAtUtc).ProjectToType<AdminFestivalView>(_mapping).ToListAsync(cancellationToken);
    var orderCounts = await _database.Orders.AsNoTracking().GroupBy(order => order.FestivalId).ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);

    return new(festivals.Select(festival => festival with
                                            {
                                              IsRunning = _runningFestival.IsRunning(festival.IsHidden, festival.StartsAtUtc, festival.EndsAtUtc),
                                              OrderCount = orderCounts.GetValueOrDefault(festival.FestivalId)
                                            })
                        .ToList());
  }

  public async Task<ErrorOr<SavedFestivalView>> CreateAsync(SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var startsAtUtc = AsUtc(request.StartsAtUtc);
    var endsAtUtc = AsUtc(request.EndsAtUtc);

    if (await PeriodRefusalAsync(startsAtUtc, endsAtUtc, Guid.Empty, cancellationToken) is { } refusal)
      return refusal;

    var created = await BuildFestivalAsync(request.Name!, startsAtUtc, endsAtUtc, cancellationToken);

    _database.Festivals.Add(created);
    await _database.SaveChangesAsync(cancellationToken);

    return created.Adapt<SavedFestivalView>(_mapping);
  }

  public async Task<ErrorOr<SavedFestivalView>> UpdateAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var festival = await _database.Festivals.FirstOrDefaultAsync(row => row.Id == festivalId, cancellationToken);

    if (festival is null)
      return Refusal.Festival.FestivalNotFound(festivalId);

    var startsAtUtc = AsUtc(request.StartsAtUtc);
    var endsAtUtc = AsUtc(request.EndsAtUtc);

    if (await PeriodRefusalAsync(startsAtUtc, endsAtUtc, festivalId, cancellationToken) is { } refusal)
      return refusal;

    festival.Name = request.Name!;
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;
    await _database.SaveChangesAsync(cancellationToken);

    return festival.Adapt<SavedFestivalView>(_mapping);
  }

  public async Task<ErrorOr<SavedFestivalView>> CopyAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!await _database.Festivals.AsNoTracking().AnyAsync(row => row.Id == festivalId, cancellationToken))
      return Refusal.Festival.FestivalNotFound(festivalId);

    var startsAtUtc = AsUtc(request.StartsAtUtc);
    var endsAtUtc = AsUtc(request.EndsAtUtc);

    if (await PeriodRefusalAsync(startsAtUtc, endsAtUtc, Guid.Empty, cancellationToken) is { } refusal)
      return refusal;

    var copy = await BuildFestivalAsync(request.Name!, startsAtUtc, endsAtUtc, cancellationToken);

    _database.Festivals.Add(copy);
    await CopyContentsAsync(festivalId, copy.Id, cancellationToken);
    await _database.SaveChangesAsync(cancellationToken);

    return copy.Adapt<SavedFestivalView>(_mapping);
  }

  public async Task<ErrorOr<SavedFestivalView>> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _database.Festivals.FirstOrDefaultAsync(row => row.Id == festivalId, cancellationToken);

    if (festival is null)
      return Refusal.Festival.FestivalNotFound(festivalId);

    if (_runningFestival.IsRunning(festival))
      return Refusal.Festival.FestivalIsRunning(festivalId);

    festival.IsHidden = true;
    await _database.SaveChangesAsync(cancellationToken);

    return festival.Adapt<SavedFestivalView>(_mapping);
  }

  private async Task<Error?> PeriodRefusalAsync(DateTime startsAtUtc, DateTime endsAtUtc, Guid festivalKeepingItsOwnPeriod, CancellationToken cancellationToken)
  {
    if (endsAtUtc <= startsAtUtc)
      return Refusal.Festival.PeriodInvalid(startsAtUtc, endsAtUtc);

    var others = await _database.Festivals.AsNoTracking().OrderByDescending(festival => festival.StartsAtUtc).ToListAsync(cancellationToken);
    var inTheWay = _schedule.FindOverlapping(festivalKeepingItsOwnPeriod, startsAtUtc, endsAtUtc, others);

    if (inTheWay is null)
      return null;

    return Refusal.Festival.PeriodOverlapsAnotherFestival(inTheWay.Name);
  }

  private async Task<Festival> BuildFestivalAsync(string name, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken cancellationToken)
  {
    Festival festival = new()
                        {
                          Id = Guid.NewGuid(),
                          Name = name,
                          StartsAtUtc = startsAtUtc,
                          EndsAtUtc = endsAtUtc,
                          NextOrderNumber = FirstNumber,
                          IsHidden = false
                        };

    var ingredientIds = await _database.Ingredients.AsNoTracking().OrderBy(ingredient => ingredient.Name).Select(ingredient => ingredient.Id).ToListAsync(cancellationToken);

    foreach (var ingredientId in ingredientIds)
      festival.Ingredients.Add(new()
                               {
                                 Id = Guid.NewGuid(),
                                 FestivalId = festival.Id,
                                 IngredientId = ingredientId,
                                 AvailableAmount = null
                               });

    return festival;
  }

  private async Task CopyContentsAsync(Guid copiedFromFestivalId, Guid newFestivalId, CancellationToken cancellationToken)
  {
    var stationLinks = await _database.FestivalStations.AsNoTracking().Where(link => link.FestivalId == copiedFromFestivalId).ToListAsync(cancellationToken);
    var linkIdsAtTheCopy = stationLinks.ToDictionary(link => link.Id, link => Guid.NewGuid());

    foreach (var link in stationLinks)
      _database.FestivalStations.Add(new()
                                     {
                                       Id = linkIdsAtTheCopy[link.Id],
                                       FestivalId = newFestivalId,
                                       StationId = link.StationId,
                                       NextStationOrderNumber = FirstNumber
                                     });

    var shownAreas = await _database.Areas.AsNoTracking().Where(area => area.FestivalId == copiedFromFestivalId && !area.IsHidden).Include(area => area.Tables.Where(table => !table.IsHidden)).Include(area => area.StationShapes).ToListAsync(cancellationToken);

    foreach (var area in shownAreas)
    {
      var areaIdAtTheCopy = Guid.NewGuid();

      _database.Areas.Add(new()
                          {
                            Id = areaIdAtTheCopy,
                            FestivalId = newFestivalId,
                            Name = area.Name,
                            SortOrder = area.SortOrder,
                            IsHidden = false
                          });

      foreach (var table in area.Tables)
      {
        Table copiedTable = new()
                            {
                              Id = Guid.NewGuid(),
                              FestivalId = newFestivalId,
                              AreaId = areaIdAtTheCopy,
                              Name = table.Name,
                              IsHidden = false
                            };

        _areaShapeService.Reshape(copiedTable, _areaShapeService.GeometryOf(table));
        _database.Tables.Add(copiedTable);
      }

      foreach (var shape in area.StationShapes)
      {
        FestivalStationShape copiedShape = new()
                                           {
                                             FestivalStationId = linkIdsAtTheCopy[shape.FestivalStationId],
                                             FestivalId = newFestivalId,
                                             AreaId = areaIdAtTheCopy
                                           };

        _areaShapeService.Reshape(copiedShape, _areaShapeService.GeometryOf(shape));
        _database.FestivalStationShapes.Add(copiedShape);
      }
    }

    var menuRows = await _database.FestivalCatalogItems.AsNoTracking().Where(menuRow => menuRow.FestivalId == copiedFromFestivalId).ToListAsync(cancellationToken);

    foreach (var menuRow in menuRows)
      _database.FestivalCatalogItems.Add(new()
                                         {
                                           Id = Guid.NewGuid(),
                                           FestivalId = newFestivalId,
                                           CatalogItemId = menuRow.CatalogItemId,
                                           PriceCents = menuRow.PriceCents,
                                           IsAvailable = true
                                         });

    var assignments = await _database.ItemStationAssignments.AsNoTracking().Where(assignment => assignment.FestivalId == copiedFromFestivalId).ToListAsync(cancellationToken);

    foreach (var assignment in assignments)
      _database.ItemStationAssignments.Add(new()
                                           {
                                             Id = Guid.NewGuid(),
                                             FestivalId = newFestivalId,
                                             CatalogItemId = assignment.CatalogItemId,
                                             StationId = assignment.StationId
                                           });
  }

  private DateTime AsUtc(DateTime moment)
  {
    if (moment.Kind == DateTimeKind.Local)
      return moment.ToUniversalTime();

    return DateTime.SpecifyKind(moment, DateTimeKind.Utc);
  }
}
