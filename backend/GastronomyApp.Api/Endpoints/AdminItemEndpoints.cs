using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminItemEndpoints
{
  public static IEndpointRouteBuilder MapAdminItemEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/items");

    group.MapGet(string.Empty, async (Guid? festivalId, AdminItemHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(festivalId, cancellationToken));

    group.MapPost(string.Empty, async (SaveItemRequest request, AdminItemHandler handler, CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken));

    group.MapPut("/{itemId:guid}", async (Guid itemId, SaveItemRequest request, AdminItemHandler handler, CancellationToken cancellationToken) => await handler.UpdateAsync(itemId, request, cancellationToken));

    group.MapPost("/{itemId:guid}/activate", async (Guid itemId, AdminItemHandler handler, CancellationToken cancellationToken) => await handler.ActivateAsync(itemId, cancellationToken));

    group.MapPost("/{itemId:guid}/deactivate", async (Guid itemId, AdminItemHandler handler, CancellationToken cancellationToken) => await handler.DeactivateAsync(itemId, cancellationToken));

    return routes;
  }
}
