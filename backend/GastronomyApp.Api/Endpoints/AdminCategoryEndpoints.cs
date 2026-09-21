using GastronomyApp.Api.Handlers;
using GastronomyApp.Contracts.Admin.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class AdminCategoryEndpoints
{
  public static IEndpointRouteBuilder MapAdminCategoryEndpoints(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/api/admin/categories");

    group.MapGet(string.Empty, async (AdminCategoryHandler handler, CancellationToken cancellationToken) => await handler.ListAsync(cancellationToken)).Produces<AdminCategoryListView>();

    group.MapPost(string.Empty, async (SaveCategoryRequest request, AdminCategoryHandler handler, CancellationToken cancellationToken) => await handler.CreateAsync(request, cancellationToken)).Produces<AdminCategoryView>(StatusCodes.Status201Created);

    group.MapPut("/{categoryId:guid}", async (Guid categoryId, SaveCategoryRequest request, AdminCategoryHandler handler, CancellationToken cancellationToken) => await handler.UpdateAsync(categoryId, request, cancellationToken)).Produces<AdminCategoryView>();

    group.MapPost("/{categoryId:guid}/move", async (Guid categoryId, MoveCategoryRequest request, AdminCategoryHandler handler, CancellationToken cancellationToken) => await handler.MoveAsync(categoryId, request, cancellationToken)).Produces<AdminCategoryListView>();

    group.MapPost("/{categoryId:guid}/activate", async (Guid categoryId, AdminCategoryHandler handler, CancellationToken cancellationToken) => await handler.ActivateAsync(categoryId, cancellationToken)).Produces<AdminCategoryView>();

    group.MapPost("/{categoryId:guid}/deactivate", async (Guid categoryId, AdminCategoryHandler handler, CancellationToken cancellationToken) => await handler.DeactivateAsync(categoryId, cancellationToken)).Produces<AdminCategoryView>();

    return routes;
  }
}
