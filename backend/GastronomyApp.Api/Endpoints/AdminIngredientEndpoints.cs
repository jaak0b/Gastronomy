using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminIngredientEndpoints
{
  public static IEndpointRouteBuilder MapAdminIngredientEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/ingredients");

    group.MapGet(string.Empty, async Task<ApiAnswer<AdminIngredientListView>> (IngredientService service, CancellationToken cancellationToken) => await service.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async Task<CreatedAnswer<AdminIngredientView>> (SaveIngredientRequest request, IngredientService service, CancellationToken cancellationToken) => await service.CreateAsync(request, cancellationToken));

    group.MapPut("/{ingredientId:guid}", async Task<ApiAnswer<SavedIngredientView>> (Guid ingredientId, SaveIngredientRequest request, IngredientService service, CancellationToken cancellationToken) => await service.UpdateAsync(ingredientId, request, cancellationToken));

    group.MapDelete("/{ingredientId:guid}", async Task<NoContentAnswer> (Guid ingredientId, IngredientService service, CancellationToken cancellationToken) => await service.DeleteAsync(ingredientId, cancellationToken));

    return routes;
  }
}
