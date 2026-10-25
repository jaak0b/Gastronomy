using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminFestivalEndpoints
{
  public static IEndpointRouteBuilder MapAdminFestivalEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/festivals");

    group.MapGet(string.Empty, async Task<ApiAnswer<AdminFestivalListView>> (FestivalService service, CancellationToken cancellationToken) => await service.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async Task<CreatedAnswer<SavedFestivalView>> (SaveFestivalRequest request, FestivalService service, CancellationToken cancellationToken) => await service.CreateAsync(request, cancellationToken));

    group.MapPut("/{festivalId:guid}", async Task<ApiAnswer<SavedFestivalView>> (Guid festivalId, SaveFestivalRequest request, FestivalService service, CancellationToken cancellationToken) => await service.UpdateAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/copy", async Task<CreatedAnswer<SavedFestivalView>> (Guid festivalId, SaveFestivalRequest request, FestivalService service, CancellationToken cancellationToken) => await service.CopyAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/hide", async Task<ApiAnswer<SavedFestivalView>> (Guid festivalId, FestivalService service, CancellationToken cancellationToken) => await service.HideAsync(festivalId, cancellationToken));

    group.MapPost("/{festivalId:guid}/show", async Task<ApiAnswer<SavedFestivalView>> (Guid festivalId, ActivationService service, CancellationToken cancellationToken) => await service.ShowAsync<Festival, SavedFestivalView>(festivalId, Refusal.Festival.FestivalNotFound, cancellationToken));

    group.MapPut("/{festivalId:guid}/items/{itemId:guid}", async (Guid festivalId, Guid itemId, SaveFestivalItemRequest request, AdminFestivalMenuHandler handler, CancellationToken cancellationToken) => await handler.PutOnTheMenuAsync(festivalId, itemId, request, cancellationToken));

    group.MapDelete("/{festivalId:guid}/items/{itemId:guid}", async (Guid festivalId, Guid itemId, AdminFestivalMenuHandler handler, CancellationToken cancellationToken) => await handler.TakeOffTheMenuAsync(festivalId, itemId, cancellationToken));

    group.MapPost("/{festivalId:guid}/items/{itemId:guid}/availability", async (Guid festivalId, Guid itemId, SetAvailabilityRequest request, AdminFestivalMenuHandler handler, CancellationToken cancellationToken) => await handler.SetAvailabilityAsync(festivalId, itemId, request, cancellationToken));

    group.MapPut("/{festivalId:guid}/stations/{stationId:guid}", async (Guid festivalId, Guid stationId, AdminFestivalStationHandler handler, CancellationToken cancellationToken) => await handler.AddAsync(festivalId, stationId, cancellationToken));

    group.MapDelete("/{festivalId:guid}/stations/{stationId:guid}", async (Guid festivalId, Guid stationId, AdminFestivalStationHandler handler, CancellationToken cancellationToken) => await handler.RemoveAsync(festivalId, stationId, cancellationToken));

    group.MapGet("/{festivalId:guid}/ingredients", async (Guid festivalId, AdminFestivalStockHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(festivalId, cancellationToken));

    group.MapPut("/{festivalId:guid}/ingredients/{ingredientId:guid}",
                 async (Guid festivalId, Guid ingredientId, SaveFestivalIngredientRequest request, AdminFestivalStockHandler handler, CancellationToken cancellationToken) => await handler.SetAvailableAmountAsync(festivalId, ingredientId, request, cancellationToken));

    group.MapGet("/{festivalId:guid}/table-plan", async Task<ApiAnswer<AdminTablePlanView>> (Guid festivalId, TablePlanService service, CancellationToken cancellationToken) => await service.ListAsync(festivalId, cancellationToken));

    group.MapPost("/{festivalId:guid}/areas", async Task<CreatedAnswer<SavedAreaView>> (Guid festivalId, SaveAreaRequest request, AreaEditService service, CancellationToken cancellationToken) => await service.CreateAsync(festivalId, request, cancellationToken));

    group.MapPut("/{festivalId:guid}/areas/{areaId:guid}", async Task<ApiAnswer<SavedAreaView>> (Guid festivalId, Guid areaId, SaveAreaRequest request, AreaEditService service, CancellationToken cancellationToken) => await service.UpdateAsync(festivalId, areaId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/areas/{areaId:guid}/move", async Task<ApiAnswer<SavedAreaView>> (Guid festivalId, Guid areaId, MoveAreaRequest request, AreaEditService service, CancellationToken cancellationToken) => await service.MoveAsync(festivalId, areaId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/areas/{areaId:guid}/hide", async Task<ApiAnswer<SavedAreaView>> (Guid festivalId, Guid areaId, AreaEditService service, CancellationToken cancellationToken) => await service.HideAsync(festivalId, areaId, cancellationToken));

    group.MapPost("/{festivalId:guid}/areas/{areaId:guid}/show",
                  async Task<ApiAnswer<SavedAreaView>> (Guid festivalId, Guid areaId, ActivationService service, CancellationToken cancellationToken) => await service.ShowAsync<Area, SavedAreaView>(festivalId, areaId, id => Refusal.TablePlan.AreaNotFound(festivalId, id), cancellationToken));

    group.MapPost("/{festivalId:guid}/tables", async Task<CreatedAnswer<SavedTableView>> (Guid festivalId, SaveTableRequest request, TableEditService service, CancellationToken cancellationToken) => await service.CreateAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/tables/copy", async Task<CreatedAnswer<CopiedTablesView>> (Guid festivalId, CopyTablesRequest request, TableEditService service, CancellationToken cancellationToken) => await service.CopyAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/tables/move", async Task<NoContentAnswer> (Guid festivalId, MoveTablesRequest request, TableEditService service, CancellationToken cancellationToken) => await service.MoveAsync(festivalId, request, cancellationToken));

    group.MapPut("/{festivalId:guid}/tables/{tableId:guid}", async Task<ApiAnswer<SavedTableView>> (Guid festivalId, Guid tableId, SaveTableRequest request, TableEditService service, CancellationToken cancellationToken) => await service.UpdateAsync(festivalId, tableId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/tables/{tableId:guid}/hide", async Task<ApiAnswer<SavedTableView>> (Guid festivalId, Guid tableId, TableEditService service, CancellationToken cancellationToken) => await service.HideAsync(festivalId, tableId, cancellationToken));

    group.MapPost("/{festivalId:guid}/tables/{tableId:guid}/show",
                  async Task<ApiAnswer<SavedTableView>> (Guid festivalId, Guid tableId, ActivationService service, CancellationToken cancellationToken) => await service.ShowAsync<Table, SavedTableView>(festivalId, tableId, id => Refusal.TablePlan.TableNotFound(festivalId, id), cancellationToken));

    group.MapPut("/{festivalId:guid}/stations/{stationId:guid}/shape",
                 async (Guid festivalId, Guid stationId, SaveStationShapeRequest request, AdminFestivalStationShapeHandler handler, CancellationToken cancellationToken) => await handler.PlaceAsync(festivalId, stationId, request, cancellationToken));

    group.MapDelete("/{festivalId:guid}/stations/{stationId:guid}/shape", async (Guid festivalId, Guid stationId, AdminFestivalStationShapeHandler handler, CancellationToken cancellationToken) => await handler.RemoveAsync(festivalId, stationId, cancellationToken));

    return routes;
  }
}
