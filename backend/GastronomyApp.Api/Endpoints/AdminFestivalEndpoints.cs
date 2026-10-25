using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Admin.Ingredients;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminFestivalEndpoints
{
  public static IEndpointRouteBuilder MapAdminFestivalEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/festivals");

    group.MapGet(string.Empty, async (AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async (SaveFestivalRequest request, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{festivalId:guid}", async (Guid festivalId, SaveFestivalRequest request, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.UpdateAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/copy", async (Guid festivalId, SaveFestivalRequest request, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.CopyAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/hide", async (Guid festivalId, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.HideAsync(festivalId, cancellationToken));

    group.MapPost("/{festivalId:guid}/show", async (Guid festivalId, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.ShowAsync(festivalId, cancellationToken));

    group.MapPut("/{festivalId:guid}/items/{itemId:guid}", async (Guid festivalId, Guid itemId, SaveFestivalItemRequest request, AdminFestivalMenuHandler handler, CancellationToken cancellationToken) => await handler.PutOnTheMenuAsync(festivalId, itemId, request, cancellationToken));

    group.MapDelete("/{festivalId:guid}/items/{itemId:guid}", async (Guid festivalId, Guid itemId, AdminFestivalMenuHandler handler, CancellationToken cancellationToken) => await handler.TakeOffTheMenuAsync(festivalId, itemId, cancellationToken));

    group.MapPost("/{festivalId:guid}/items/{itemId:guid}/availability", async (Guid festivalId, Guid itemId, SetAvailabilityRequest request, AdminFestivalMenuHandler handler, CancellationToken cancellationToken) => await handler.SetAvailabilityAsync(festivalId, itemId, request, cancellationToken));

    group.MapPut("/{festivalId:guid}/stations/{stationId:guid}", async (Guid festivalId, Guid stationId, AdminFestivalStationHandler handler, CancellationToken cancellationToken) => await handler.AddAsync(festivalId, stationId, cancellationToken));

    group.MapDelete("/{festivalId:guid}/stations/{stationId:guid}", async (Guid festivalId, Guid stationId, AdminFestivalStationHandler handler, CancellationToken cancellationToken) => await handler.RemoveAsync(festivalId, stationId, cancellationToken));

    group.MapGet("/{festivalId:guid}/ingredients", async (Guid festivalId, AdminFestivalStockHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(festivalId, cancellationToken));

    group.MapPut("/{festivalId:guid}/ingredients/{ingredientId:guid}",
                 async (Guid festivalId, Guid ingredientId, SaveFestivalIngredientRequest request, AdminFestivalStockHandler handler, CancellationToken cancellationToken) => await handler.SetAvailableAmountAsync(festivalId, ingredientId, request, cancellationToken));

    group.MapGet("/{festivalId:guid}/table-plan", async (Guid festivalId, AdminAreaHandler handler, CancellationToken cancellationToken) => await handler.ListTablePlanAsync(festivalId, cancellationToken));

    group.MapPost("/{festivalId:guid}/areas", async (Guid festivalId, SaveAreaRequest request, AdminAreaHandler handler, CancellationToken cancellationToken) => await handler.CreateAsync(festivalId, request, cancellationToken));

    group.MapPut("/{festivalId:guid}/areas/{areaId:guid}", async (Guid festivalId, Guid areaId, SaveAreaRequest request, AdminAreaHandler handler, CancellationToken cancellationToken) => await handler.UpdateAsync(festivalId, areaId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/areas/{areaId:guid}/move", async (Guid festivalId, Guid areaId, MoveAreaRequest request, AdminAreaHandler handler, CancellationToken cancellationToken) => await handler.MoveAsync(festivalId, areaId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/areas/{areaId:guid}/hide", async (Guid festivalId, Guid areaId, AdminAreaHandler handler, CancellationToken cancellationToken) => await handler.HideAsync(festivalId, areaId, cancellationToken));

    group.MapPost("/{festivalId:guid}/areas/{areaId:guid}/show", async (Guid festivalId, Guid areaId, AdminAreaHandler handler, CancellationToken cancellationToken) => await handler.ShowAsync(festivalId, areaId, cancellationToken));

    group.MapPost("/{festivalId:guid}/tables", async (Guid festivalId, SaveTableRequest request, AdminTableHandler handler, CancellationToken cancellationToken) => await handler.CreateAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/tables/copy", async (Guid festivalId, CopyTablesRequest request, AdminTableHandler handler, CancellationToken cancellationToken) => await handler.CopyAsync(festivalId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/tables/move", async (Guid festivalId, MoveTablesRequest request, AdminTableHandler handler, CancellationToken cancellationToken) => await handler.MoveAsync(festivalId, request, cancellationToken));

    group.MapPut("/{festivalId:guid}/tables/{tableId:guid}", async (Guid festivalId, Guid tableId, SaveTableRequest request, AdminTableHandler handler, CancellationToken cancellationToken) => await handler.UpdateAsync(festivalId, tableId, request, cancellationToken));

    group.MapPost("/{festivalId:guid}/tables/{tableId:guid}/hide", async (Guid festivalId, Guid tableId, AdminTableHandler handler, CancellationToken cancellationToken) => await handler.HideAsync(festivalId, tableId, cancellationToken));

    group.MapPost("/{festivalId:guid}/tables/{tableId:guid}/show", async (Guid festivalId, Guid tableId, AdminTableHandler handler, CancellationToken cancellationToken) => await handler.ShowAsync(festivalId, tableId, cancellationToken));

    group.MapPut("/{festivalId:guid}/stations/{stationId:guid}/shape",
                 async (Guid festivalId, Guid stationId, SaveStationShapeRequest request, AdminFestivalStationShapeHandler handler, CancellationToken cancellationToken) => await handler.PlaceAsync(festivalId, stationId, request, cancellationToken));

    group.MapDelete("/{festivalId:guid}/stations/{stationId:guid}/shape", async (Guid festivalId, Guid stationId, AdminFestivalStationShapeHandler handler, CancellationToken cancellationToken) => await handler.RemoveAsync(festivalId, stationId, cancellationToken));

    return routes;
  }
}
