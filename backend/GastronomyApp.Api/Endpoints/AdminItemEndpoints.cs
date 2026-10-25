using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminItemEndpoints
{
  public static IEndpointRouteBuilder MapAdminItemEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/items");

    group.MapGet(string.Empty, async Task<ApiAnswer<AdminItemListView>> (Guid? festivalId, CatalogItemService service, CancellationToken cancellationToken) => await service.ListAsync(festivalId, cancellationToken));

    group.MapPost(string.Empty, async Task<CreatedAnswer<AdminItemView>> (SaveItemRequest request, CatalogItemService service, CancellationToken cancellationToken) => await service.CreateAsync(request, cancellationToken));

    group.MapPut("/{itemId:guid}", async Task<ApiAnswer<SavedItemView>> (Guid itemId, SaveItemRequest request, CatalogItemService service, CancellationToken cancellationToken) => await service.UpdateAsync(itemId, request, cancellationToken));

    group.MapPost("/{itemId:guid}/activate", async Task<ApiAnswer<SavedItemView>> (Guid itemId, CatalogItemService service, CancellationToken cancellationToken) => await service.ActivateAsync(itemId, cancellationToken));

    group.MapPost("/{itemId:guid}/deactivate", async Task<ApiAnswer<SavedItemView>> (Guid itemId, CatalogItemService service, CancellationToken cancellationToken) => await service.DeactivateAsync(itemId, cancellationToken));

    group.MapPut("/{itemId:guid}/ingredients/{ingredientId:guid}",
                 async Task<ApiAnswer<SavedItemView>> (Guid itemId, Guid ingredientId, SaveItemIngredientRequest request, CatalogItemService service, CancellationToken cancellationToken) => await service.SetRecipeLineAsync(itemId, ingredientId, request, cancellationToken));

    group.MapDelete("/{itemId:guid}/ingredients/{ingredientId:guid}", async Task<NoContentAnswer> (Guid itemId, Guid ingredientId, CatalogItemService service, CancellationToken cancellationToken) => await service.RemoveRecipeLineAsync(itemId, ingredientId, cancellationToken));

    return routes;
  }
}
