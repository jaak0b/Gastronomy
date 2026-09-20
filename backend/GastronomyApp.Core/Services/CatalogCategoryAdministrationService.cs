using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class CatalogCategoryAdministrationService
{
  private readonly ColorFormatValidator _colour;
  private readonly CatalogCategoryOrdering _ordering;
  private readonly ICatalogCategoryRepository _repository;
  private readonly ITransactionRunner _transactionRunner;

  public CatalogCategoryAdministrationService(ICatalogCategoryRepository repository,
                                              CatalogCategoryOrdering ordering,
                                              ColorFormatValidator colour,
                                              ITransactionRunner transactionRunner)
  {
    _repository = repository;
    _ordering = ordering;
    _colour = colour;
    _transactionRunner = transactionRunner;
  }

  public Task<IReadOnlyList<CatalogCategory>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAllOrderedAsync(cancellationToken);
  }

  public Task<Result<CatalogCategory, CatalogCategoryAdministrationFailure>> CreateAsync(
    SaveCatalogCategoryRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return RunAsync(transactionCancellationToken => CreatedAsync(request, transactionCancellationToken),
                    written => written.IsSuccess,
                    cancellationToken);
  }

  public Task<Result<CatalogCategory, CatalogCategoryAdministrationFailure>> UpdateAsync(
    Guid categoryId,
    SaveCatalogCategoryRequest request,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return RunAsync(transactionCancellationToken => UpdatedAsync(categoryId, request, transactionCancellationToken),
                    written => written.IsSuccess,
                    cancellationToken);
  }

  public Task<Result<ReorderedCatalogCategories, CatalogCategoryAdministrationFailure>> MoveAsync(
    Guid categoryId,
    CategoryMoveDirection direction,
    CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => MovedAsync(categoryId, direction, transactionCancellationToken),
                    written => written.IsSuccess && written.Value.OrderChanged,
                    cancellationToken);
  }

  public Task<Result<CatalogCategory, CatalogCategoryAdministrationFailure>> ActivateAsync(
    Guid categoryId,
    CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(categoryId, transactionCancellationToken),
                    written => written.IsSuccess,
                    cancellationToken);
  }

  public Task<Result<CatalogCategory, CatalogCategoryAdministrationFailure>> DeactivateAsync(
    Guid categoryId,
    CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(categoryId, transactionCancellationToken),
                    written => written.IsSuccess,
                    cancellationToken);
  }

  private async Task<Result<CatalogCategory, CatalogCategoryAdministrationFailure>> CreatedAsync(
    SaveCatalogCategoryRequest request,
    CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);
    CatalogCategoryAdministrationFailure? refusal = Validate(request, categories, null);

    if (refusal is not null)
    {
      return Result<CatalogCategory, CatalogCategoryAdministrationFailure>.Failed(refusal);
    }

    CatalogCategory created = new()
                              {
                                Id = Guid.NewGuid(),
                                Name = request.Name!.Trim(),
                                ColourHex = request.ColourHex!,
                                SortOrder = _ordering.NextSortOrder([.. categories.Select(category => category.SortOrder)]),
                                IsActive = true
                              };

    await _repository.AddAsync(created, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<CatalogCategory, CatalogCategoryAdministrationFailure>.Success(created);
  }

  private async Task<Result<CatalogCategory, CatalogCategoryAdministrationFailure>> UpdatedAsync(
    Guid categoryId,
    SaveCatalogCategoryRequest request,
    CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);
    CatalogCategory? category = categories.FirstOrDefault(candidate => candidate.Id == categoryId);

    if (category is null)
    {
      return Failed<CatalogCategory>(CatalogCategoryAdministrationFailureReason.CategoryNotFound);
    }

    CatalogCategoryAdministrationFailure? refusal = Validate(request, categories, categoryId);

    if (refusal is not null)
    {
      return Result<CatalogCategory, CatalogCategoryAdministrationFailure>.Failed(refusal);
    }

    category.Name = request.Name!.Trim();
    category.ColourHex = request.ColourHex!;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<CatalogCategory, CatalogCategoryAdministrationFailure>.Success(category);
  }

  private async Task<Result<ReorderedCatalogCategories, CatalogCategoryAdministrationFailure>> MovedAsync(
    Guid categoryId,
    CategoryMoveDirection direction,
    CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);

    if (categories.All(candidate => candidate.Id != categoryId))
    {
      return Failed<ReorderedCatalogCategories>(CatalogCategoryAdministrationFailureReason.CategoryNotFound);
    }

    Dictionary<Guid, CatalogCategory> categoriesById = categories.ToDictionary(category => category.Id);

    IReadOnlyList<CatalogCategoryPosition> positions =
      _ordering.Move([.. categories.Select(category => category.Id)], categoryId, direction);

    IReadOnlyList<CatalogCategory> reordered =
      [.. positions.Select(position => categoriesById[position.CategoryId])];

    if (positions.All(position => categoriesById[position.CategoryId].SortOrder == position.SortOrder))
    {
      return Result<ReorderedCatalogCategories, CatalogCategoryAdministrationFailure>.Success(new(reordered, false));
    }

    foreach (var position in positions)
    {
      categoriesById[position.CategoryId].SortOrder = position.SortOrder;
    }

    await _repository.SaveChangesAsync(cancellationToken);

    return Result<ReorderedCatalogCategories, CatalogCategoryAdministrationFailure>.Success(new(reordered, true));
  }

  private async Task<Result<CatalogCategory, CatalogCategoryAdministrationFailure>> SwitchedOnAsync(
    Guid categoryId,
    CancellationToken cancellationToken)
  {
    CatalogCategory? category = await _repository.FindByIdAsync(categoryId, cancellationToken);

    if (category is null)
    {
      return Failed<CatalogCategory>(CatalogCategoryAdministrationFailureReason.CategoryNotFound);
    }

    category.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<CatalogCategory, CatalogCategoryAdministrationFailure>.Success(category);
  }

  private async Task<Result<CatalogCategory, CatalogCategoryAdministrationFailure>> SwitchedOffAsync(
    Guid categoryId,
    CancellationToken cancellationToken)
  {
    CatalogCategory? category = await _repository.FindByIdAsync(categoryId, cancellationToken);

    if (category is null)
    {
      return Failed<CatalogCategory>(CatalogCategoryAdministrationFailureReason.CategoryNotFound);
    }

    if (await _repository.HoldsActiveItemsAsync(categoryId, cancellationToken))
    {
      return Failed<CatalogCategory>(CatalogCategoryAdministrationFailureReason.CategoryHoldsActiveItems);
    }

    category.IsActive = false;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<CatalogCategory, CatalogCategoryAdministrationFailure>.Success(category);
  }

  private CatalogCategoryAdministrationFailure? Validate(SaveCatalogCategoryRequest request,
                                                         IReadOnlyCollection<CatalogCategory> categories,
                                                         Guid? categoryBeingSaved)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
    {
      return new() { Reason = CatalogCategoryAdministrationFailureReason.NameMissing };
    }

    if (!_colour.IsWellFormed(request.ColourHex))
    {
      return new() { Reason = CatalogCategoryAdministrationFailureReason.ColourInvalid };
    }

    var wantedName = request.Name.Trim();

    var taken = categories.Any(category => category.Id != categoryBeingSaved
                                           && string.Equals(category.Name,
                                                            wantedName,
                                                            StringComparison.OrdinalIgnoreCase));

    return taken
             ? new() { Reason = CatalogCategoryAdministrationFailureReason.NameTaken }
             : null;
  }

  private Result<TValue, CatalogCategoryAdministrationFailure> Failed<TValue>(
    CatalogCategoryAdministrationFailureReason reason)
  {
    return Result<TValue, CatalogCategoryAdministrationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<TValue, CatalogCategoryAdministrationFailure>> RunAsync<TValue>(
    Func<CancellationToken, Task<Result<TValue, CatalogCategoryAdministrationFailure>>> write,
    Func<Result<TValue, CatalogCategoryAdministrationFailure>, bool> shouldCommit,
    CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<TValue, CatalogCategoryAdministrationFailure> written =
                                                 await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<TValue, CatalogCategoryAdministrationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = shouldCommit(written)
                                                      };
                                             },
                                             cancellationToken);
  }
}
