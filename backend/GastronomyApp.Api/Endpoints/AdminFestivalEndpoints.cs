using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Admin.Stations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminFestivalEndpoints
{
  public static IEndpointRouteBuilder MapAdminFestivalEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/festivals");

    group.MapGet(string.Empty, async (AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken)).Produces<AdminFestivalListView>();

    group.MapPost(string.Empty, async (SaveFestivalRequest request, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken)).Produces<SavedFestivalView>(StatusCodes.Status201Created);

    group.MapPut("/{festivalId:guid}", async (Guid festivalId, SaveFestivalRequest request, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.UpdateAsync(festivalId, request, cancellationToken)).Produces<SavedFestivalView>();

    group.MapPost("/{festivalId:guid}/copy", async (Guid festivalId, SaveFestivalRequest request, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.CopyAsync(festivalId, request, cancellationToken)).Produces<SavedFestivalView>(StatusCodes.Status201Created);

    group.MapPost("/{festivalId:guid}/hide", async (Guid festivalId, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.HideAsync(festivalId, cancellationToken)).Produces<SavedFestivalView>();

    group.MapPost("/{festivalId:guid}/show", async (Guid festivalId, AdminFestivalHandler handler, CancellationToken cancellationToken) => await handler.ShowAsync(festivalId, cancellationToken)).Produces<SavedFestivalView>();

    group.MapPut("/{festivalId:guid}/items/{itemId:guid}", async (Guid festivalId, Guid itemId, SaveFestivalItemRequest request, AdminFestivalMenuHandler handler, CancellationToken cancellationToken) => await handler.PutOnTheMenuAsync(festivalId, itemId, request, cancellationToken)).Produces<SavedItemView>();

    group.MapDelete("/{festivalId:guid}/items/{itemId:guid}", async (Guid festivalId, Guid itemId, AdminFestivalMenuHandler handler, CancellationToken cancellationToken) => await handler.TakeOffTheMenuAsync(festivalId, itemId, cancellationToken)).Produces(StatusCodes.Status204NoContent);

    group.MapPost("/{festivalId:guid}/items/{itemId:guid}/availability", async (Guid festivalId, Guid itemId, SetAvailabilityRequest request, AdminFestivalMenuHandler handler, CancellationToken cancellationToken) => await handler.SetAvailabilityAsync(festivalId, itemId, request, cancellationToken)).Produces<SavedItemView>();

    group.MapPut("/{festivalId:guid}/stations/{stationId:guid}", async (Guid festivalId, Guid stationId, AdminFestivalStationHandler handler, CancellationToken cancellationToken) => await handler.AddAsync(festivalId, stationId, cancellationToken)).Produces<SavedStationView>();

    group.MapDelete("/{festivalId:guid}/stations/{stationId:guid}", async (Guid festivalId, Guid stationId, AdminFestivalStationHandler handler, CancellationToken cancellationToken) => await handler.RemoveAsync(festivalId, stationId, cancellationToken)).Produces(StatusCodes.Status204NoContent);

    return routes;
  }
}
