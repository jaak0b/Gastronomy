using ErrorOr;
using GastronomyApp.Contracts.Admin.Stations;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class StationService
{
  private readonly IGastronomyDatabase _database;
  private readonly ItemsToHandOutLookup _itemsToHandOut;
  private readonly TypeAdapterConfig _mapping;
  private readonly ItemOrderability _orderability;
  private readonly DeviceOwnerRetirement _retirement;

  public StationService(IGastronomyDatabase database, TypeAdapterConfig mapping, DeviceOwnerRetirement retirement, ItemOrderability orderability, ItemsToHandOutLookup itemsToHandOut)
  {
    _database = database;
    _mapping = mapping;
    _retirement = retirement;
    _orderability = orderability;
    _itemsToHandOut = itemsToHandOut;
  }

  public async Task<ErrorOr<AdminStationListView>> ListAsync(Guid? festivalId, CancellationToken cancellationToken)
  {
    if (festivalId is not { } askedFestivalId)
      return await ListWithoutFestivalAsync(cancellationToken);

    var festival = await _database.Festivals.AsNoTracking().FirstOrDefaultAsync(row => row.Id == askedFestivalId, cancellationToken);

    if (festival is null)
      return Refusal.Station.FestivalNotFound(askedFestivalId);

    var stations = await ReadStationsAsync(cancellationToken);
    var stationIds = stations.Select(station => station.StationId).ToList();
    var stationIdsAtTheFestival = await _database.FestivalStations.AsNoTracking().Where(link => link.FestivalId == askedFestivalId).Select(link => link.StationId).ToListAsync(cancellationToken);
    var holdingItems = await _itemsToHandOut.FindStationIdsIfRunningAsync(festival, cancellationToken);
    var strandedByRemoving = await _orderability.FindItemsStrandedByRemovingEachStationAsync(askedFestivalId, stationIds, cancellationToken);

    return new AdminStationListView(stations.Select(station => station with
                                                               {
                                                                 IsAtFestival = stationIdsAtTheFestival.Contains(station.StationId),
                                                                 HasItemsToHandOutAtTheRunningFestival = holdingItems.Contains(station.StationId),
                                                                 StrandedItemCountWhenRemovedFromTheFestival = strandedByRemoving[station.StationId].Count
                                                               })
                                            .ToList());
  }

  public async Task<ErrorOr<AdminStationView>> CreateAsync(SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Station created = new()
                      {
                        Id = Guid.NewGuid(),
                        Name = request.Name!,
                        SortOrder = request.SortOrder,
                        IsActive = true
                      };

    _database.Stations.Add(created);
    await _database.SaveChangesAsync(cancellationToken);

    return created.Adapt<AdminStationView>(_mapping) with
           {
             StrandedItemCountWhenSwitchedOff = 0,
             StrandedItemCountWhenRemovedFromTheFestival = 0
           };
  }

  public async Task<ErrorOr<SavedStationView>> UpdateAsync(Guid stationId, SaveStationRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var station = await _database.Stations.FirstOrDefaultAsync(row => row.Id == stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    station.Name = request.Name!;
    station.SortOrder = request.SortOrder;
    await _database.SaveChangesAsync(cancellationToken);

    return station.Adapt<SavedStationView>(_mapping);
  }

  public async Task<ErrorOr<SavedStationView>> DeactivateAsync(Guid stationId, CancellationToken cancellationToken)
  {
    var station = await _database.Stations.FirstOrDefaultAsync(row => row.Id == stationId, cancellationToken);

    if (station is null)
      return Refusal.Station.StationNotFound(stationId);

    if ((await _itemsToHandOut.FindStationIdsAtTheRunningFestivalAsync(cancellationToken)).Contains(stationId))
      return Refusal.Station.StationHasUnfulfilledItems(stationId);

    var strandedItemIds = await _orderability.FindItemsStrandedBySwitchingOffStationAsync(stationId, cancellationToken);

    if (strandedItemIds.Count > 0)
      return Refusal.Station.ItemsWouldHaveNoStation(strandedItemIds.Count);

    var deviceId = station.DeviceId;

    station.IsActive = false;
    await _retirement.WithdrawOutstandingInvitationAsync(station.EnrolmentInvitationId, cancellationToken);
    station.EnrolmentInvitationId = null;
    await _database.SaveChangesAsync(cancellationToken);

    await _retirement.RevokeDeviceAsync(deviceId, cancellationToken);

    return station.Adapt<SavedStationView>(_mapping);
  }

  private async Task<ErrorOr<AdminStationListView>> ListWithoutFestivalAsync(CancellationToken cancellationToken)
  {
    var stations = await ReadStationsAsync(cancellationToken);
    var holdingItems = await _itemsToHandOut.FindStationIdsAtTheRunningFestivalAsync(cancellationToken);
    var strandedBySwitchingOff = await _orderability.FindItemsStrandedBySwitchingOffEachStationAsync(stations.Select(station => station.StationId).ToList(), cancellationToken);

    return new AdminStationListView(stations.Select(station => station with
                                                               {
                                                                 HasItemsToHandOutAtTheRunningFestival = holdingItems.Contains(station.StationId),
                                                                 StrandedItemCountWhenSwitchedOff = strandedBySwitchingOff[station.StationId].Count
                                                               })
                                            .ToList());
  }

  private async Task<List<AdminStationView>> ReadStationsAsync(CancellationToken cancellationToken)
  {
    return await _database.Stations.AsNoTracking().OrderBy(station => station.SortOrder).ProjectToType<AdminStationView>(_mapping).ToListAsync(cancellationToken);
  }
}
