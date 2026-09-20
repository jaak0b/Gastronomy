using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Endpoints;

public sealed class AdminCategoryHandler
{
  private readonly SavedChangeAnnouncement _announcement;
  private readonly CatalogChangeAnnouncer _announcer;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly CatalogCategoryAdministrationService _service;

  public AdminCategoryHandler(CatalogCategoryAdministrationService service,
                              CatalogChangeAnnouncer announcer,
                              SavedChangeAnnouncement announcement,
                              ResultEnvelope resultEnvelope)
  {
    _service = service;
    _announcer = announcer;
    _announcement = announcement;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _service.ListAsync(cancellationToken);

    return Results.Ok(BuildCategoryListView(categories));
  }

  public async Task<IResult> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<CatalogCategory, CatalogCategoryAdministrationFailure> created =
      await _service.CreateAsync(BuildSaveRequest(request), cancellationToken);

    return await AnsweredAsync(created,
                               category => Results.Json(BuildCategoryView(category),
                                                        statusCode: StatusCodes.Status201Created));
  }

  public async Task<IResult> UpdateAsync(Guid categoryId,
                                         SaveCategoryRequest request,
                                         CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<CatalogCategory, CatalogCategoryAdministrationFailure> updated =
      await _service.UpdateAsync(categoryId, BuildSaveRequest(request), cancellationToken);

    return await AnsweredAsync(updated, category => Results.Ok(BuildCategoryView(category)));
  }

  public async Task<IResult> MoveAsync(Guid categoryId,
                                       MoveCategoryRequest request,
                                       CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<ReorderedCatalogCategories, CatalogCategoryAdministrationFailure> moved =
      await _service.MoveAsync(categoryId, request.Direction, cancellationToken);

    if (!moved.IsSuccess)
    {
      return RefusalFor(moved.Failure);
    }

    if (moved.Value.OrderChanged)
    {
      await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);
    }

    return Results.Ok(BuildCategoryListView(moved.Value.Categories));
  }

  public async Task<IResult> ActivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    Result<CatalogCategory, CatalogCategoryAdministrationFailure> switchedOn =
      await _service.ActivateAsync(categoryId, cancellationToken);

    return await AnsweredAsync(switchedOn, category => Results.Ok(BuildCategoryView(category)));
  }

  public async Task<IResult> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    Result<CatalogCategory, CatalogCategoryAdministrationFailure> switchedOff =
      await _service.DeactivateAsync(categoryId, cancellationToken);

    return await AnsweredAsync(switchedOff, category => Results.Ok(BuildCategoryView(category)));
  }

  private SaveCatalogCategoryRequest BuildSaveRequest(SaveCategoryRequest request)
  {
    return new()
           {
             Name = request.Name,
             ColourHex = request.ColourHex
           };
  }

  private async Task<IResult> AnsweredAsync(Result<CatalogCategory, CatalogCategoryAdministrationFailure> written,
                                            Func<CatalogCategory, IResult> buildResponse)
  {
    if (!written.IsSuccess)
    {
      return RefusalFor(written.Failure);
    }

    await _announcement.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);

    return buildResponse(written.Value);
  }

  private IResult RefusalFor(CatalogCategoryAdministrationFailure failure)
  {
    return failure.Reason switch
           {
             CatalogCategoryAdministrationFailureReason.CategoryNotFound => Results.NotFound(),
             CatalogCategoryAdministrationFailureReason.NameMissing =>
               _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                       "ValidationFailed",
                                       "admin.categoryNameMissing"),
             CatalogCategoryAdministrationFailureReason.ColourInvalid =>
               _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                       "ValidationFailed",
                                       "admin.categoryColourInvalid"),
             CatalogCategoryAdministrationFailureReason.NameTaken =>
               _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                       "CategoryNameTaken",
                                       "admin.categoryNameTaken"),
             CatalogCategoryAdministrationFailureReason.CategoryHoldsActiveItems =>
               _resultEnvelope.Problem(StatusCodes.Status409Conflict,
                                       "CategoryHasActiveItems",
                                       "admin.categoryHasActiveItems"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }

  private AdminCategoryListView BuildCategoryListView(IReadOnlyCollection<CatalogCategory> categories)
  {
    return new([.. categories.Select(BuildCategoryView)]);
  }

  private AdminCategoryView BuildCategoryView(CatalogCategory category)
  {
    return new(category.Id, category.Name, category.ColourHex, category.SortOrder, category.IsActive);
  }
}
