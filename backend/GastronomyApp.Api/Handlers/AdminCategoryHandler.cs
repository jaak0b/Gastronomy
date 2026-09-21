using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminCategoryHandler
{
  private readonly CatalogChangeAnnouncer _announcer;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly SavedChangeAnnouncer _savedChangeAnnouncer;
  private readonly CatalogCategoryAdministrationService _service;

  public AdminCategoryHandler(CatalogCategoryAdministrationService service, CatalogChangeAnnouncer announcer, SavedChangeAnnouncer savedChangeAnnouncer, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _announcer = announcer;
    _savedChangeAnnouncer = savedChangeAnnouncer;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _service.ListAsync(cancellationToken);

    return Results.Ok(BuildCategoryListView(categories));
  }

  public async Task<IResult> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> created = await _service.CreateAsync(request.Name, request.ColourHex, cancellationToken);

    return await AnsweredAsync(created, category => Results.Json(_mapper.Map<AdminCategoryView>(category), statusCode: StatusCodes.Status201Created));
  }

  public async Task<IResult> UpdateAsync(Guid categoryId, SaveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> updated = await _service.UpdateAsync(categoryId, request.Name, request.ColourHex, cancellationToken);

    return await AnsweredAsync(updated, category => Results.Ok(_mapper.Map<AdminCategoryView>(category)));
  }

  public async Task<IResult> MoveAsync(Guid categoryId, MoveCategoryRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<IReadOnlyList<CatalogCategory>?, Failure<CatalogCategoryAdministrationFailureReason>> moved = await _service.MoveAsync(categoryId, request.Direction, cancellationToken);

    if (!moved.IsSuccess)
      return RefusalFor(moved.Failure);

    if (moved.Value is not { } reordered)
      return Results.Ok(BuildCategoryListView(await _service.ListAsync(cancellationToken)));

    await _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);

    return Results.Ok(BuildCategoryListView(reordered));
  }

  public async Task<IResult> ActivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> switchedOn = await _service.ActivateAsync(categoryId, cancellationToken);

    return await AnsweredAsync(switchedOn, category => Results.Ok(_mapper.Map<AdminCategoryView>(category)));
  }

  public async Task<IResult> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> switchedOff = await _service.DeactivateAsync(categoryId, cancellationToken);

    return await AnsweredAsync(switchedOff, category => Results.Ok(_mapper.Map<AdminCategoryView>(category)));
  }

  private async Task<IResult> AnsweredAsync(Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>> written, Func<CatalogCategory, IResult> buildResponse)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure);

    await _savedChangeAnnouncer.TellTheDevicesWithoutFailingTheSavedChangeAsync(_announcer.AnnounceAsync);

    return buildResponse(written.Value);
  }

  private IResult RefusalFor(Failure<CatalogCategoryAdministrationFailureReason> failure)
  {
    return failure.Reason switch
           {
             CatalogCategoryAdministrationFailureReason.CategoryNotFound => Results.NotFound(),
             CatalogCategoryAdministrationFailureReason.NameMissing => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.categoryNameMissing"),
             CatalogCategoryAdministrationFailureReason.ColourInvalid => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.categoryColourInvalid"),
             CatalogCategoryAdministrationFailureReason.NameTaken => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "CategoryNameTaken", "admin.categoryNameTaken"),
             CatalogCategoryAdministrationFailureReason.CategoryHoldsActiveItems => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "CategoryHasActiveItems", "admin.categoryHasActiveItems"),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }

  private AdminCategoryListView BuildCategoryListView(IReadOnlyCollection<CatalogCategory> categories)
  {
    return new(_mapper.Map<IReadOnlyList<AdminCategoryView>>(categories));
  }
}
