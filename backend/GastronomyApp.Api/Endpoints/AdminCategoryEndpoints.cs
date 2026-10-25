using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Catalog;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminCategoryEndpoints
{
  public static IEndpointRouteBuilder MapAdminCategoryEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/categories");

    group.MapGet(string.Empty, async Task<ApiAnswer<AdminCategoryListView>> (CatalogCategoryService service, CancellationToken cancellationToken) => await service.ListAsync(cancellationToken));

    group.MapPost(string.Empty, async Task<CreatedAnswer<AdminCategoryView>> (SaveCategoryRequest request, CatalogCategoryService service, CancellationToken cancellationToken) => await service.CreateAsync(request, cancellationToken));

    group.MapPut("/{categoryId:guid}", async Task<ApiAnswer<AdminCategoryView>> (Guid categoryId, SaveCategoryRequest request, CatalogCategoryService service, CancellationToken cancellationToken) => await service.UpdateAsync(categoryId, request, cancellationToken));

    group.MapPost("/{categoryId:guid}/move", async Task<ApiAnswer<AdminCategoryListView>> (Guid categoryId, MoveCategoryRequest request, CatalogCategoryService service, CancellationToken cancellationToken) => await service.MoveAsync(categoryId, request.Direction, cancellationToken));

    group.MapPost("/{categoryId:guid}/activate",
                  async Task<ApiAnswer<AdminCategoryView>> (Guid categoryId, ActivationService service, CancellationToken cancellationToken) => await service.ActivateAsync<CatalogCategory, AdminCategoryView>(categoryId, Refusal.CatalogCategory.CategoryNotFound, cancellationToken));

    group.MapPost("/{categoryId:guid}/deactivate", async Task<ApiAnswer<AdminCategoryView>> (Guid categoryId, CatalogCategoryService service, CancellationToken cancellationToken) => await service.DeactivateAsync(categoryId, cancellationToken));

    return routes;
  }
}
