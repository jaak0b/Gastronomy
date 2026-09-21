using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
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

  public CatalogCategoryAdministrationService(ICatalogCategoryRepository repository, CatalogCategoryOrdering ordering, ColorFormatValidator colour, ITransactionRunner transactionRunner)
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

  public Task<Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>> CreateAsync(string? name, string? colourHex, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => CreatedAsync(name, colourHex, transactionCancellationToken), written => written.IsSuccess, cancellationToken);
  }

  public Task<Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>> UpdateAsync(Guid categoryId, string? name, string? colourHex, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => UpdatedAsync(categoryId, name, colourHex, transactionCancellationToken), written => written.IsSuccess, cancellationToken);
  }

  public Task<Result<ReorderedCatalogCategories, Failure<CatalogCategoryAdministrationFailureReason>>> MoveAsync(Guid categoryId, CategoryMoveDirection direction, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => MovedAsync(categoryId, direction, transactionCancellationToken), written => written.IsSuccess && written.Value.OrderChanged, cancellationToken);
  }

  public Task<Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>> ActivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOnAsync(categoryId, transactionCancellationToken), written => written.IsSuccess, cancellationToken);
  }

  public Task<Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => SwitchedOffAsync(categoryId, transactionCancellationToken), written => written.IsSuccess, cancellationToken);
  }

  private async Task<Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>> CreatedAsync(string? name, string? colourHex, CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);
    Failure<CatalogCategoryAdministrationFailureReason>? refusal = Validate(name, colourHex, categories, null);

    if (refusal is not null)
      return Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>.Failed(refusal);

    CatalogCategory created = new()
                              {
                                Id = Guid.NewGuid(),
                                Name = name!.Trim(),
                                ColourHex = colourHex!,
                                SortOrder = _ordering.NextSortOrder(categories.Select(category => category.SortOrder).ToList()),
                                IsActive = true
                              };

    await _repository.AddAsync(created, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>.Success(created);
  }

  private async Task<Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>> UpdatedAsync(Guid categoryId, string? name, string? colourHex, CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);
    var category = categories.FirstOrDefault(candidate => candidate.Id == categoryId);

    if (category is null)
      return Failed<CatalogCategory>(CatalogCategoryAdministrationFailureReason.CategoryNotFound);

    Failure<CatalogCategoryAdministrationFailureReason>? refusal = Validate(name, colourHex, categories, categoryId);

    if (refusal is not null)
      return Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>.Failed(refusal);

    category.Name = name!.Trim();
    category.ColourHex = colourHex!;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>.Success(category);
  }

  private async Task<Result<ReorderedCatalogCategories, Failure<CatalogCategoryAdministrationFailureReason>>> MovedAsync(Guid categoryId, CategoryMoveDirection direction, CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);

    if (categories.All(candidate => candidate.Id != categoryId))
      return Failed<ReorderedCatalogCategories>(CatalogCategoryAdministrationFailureReason.CategoryNotFound);

    Dictionary<Guid, CatalogCategory> categoriesById = categories.ToDictionary(category => category.Id);

    IReadOnlyList<CatalogCategoryPosition> positions = _ordering.Move(categories.Select(category => category.Id).ToList(), categoryId, direction);

    IReadOnlyList<CatalogCategory> reordered = positions.Select(position => categoriesById[position.CategoryId]).ToList();

    if (positions.All(position => categoriesById[position.CategoryId].SortOrder == position.SortOrder))
      return Result<ReorderedCatalogCategories, Failure<CatalogCategoryAdministrationFailureReason>>.Success(new(reordered, false));

    foreach (var position in positions)
      categoriesById[position.CategoryId].SortOrder = position.SortOrder;

    await _repository.SaveChangesAsync(cancellationToken);

    return Result<ReorderedCatalogCategories, Failure<CatalogCategoryAdministrationFailureReason>>.Success(new(reordered, true));
  }

  private async Task<Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>> SwitchedOnAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    var category = await _repository.FindByIdAsync(categoryId, cancellationToken);

    if (category is null)
      return Failed<CatalogCategory>(CatalogCategoryAdministrationFailureReason.CategoryNotFound);

    category.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>.Success(category);
  }

  private async Task<Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>> SwitchedOffAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    var category = await _repository.FindByIdAsync(categoryId, cancellationToken);

    if (category is null)
      return Failed<CatalogCategory>(CatalogCategoryAdministrationFailureReason.CategoryNotFound);

    if (await _repository.HoldsActiveItemsAsync(categoryId, cancellationToken))
      return Failed<CatalogCategory>(CatalogCategoryAdministrationFailureReason.CategoryHoldsActiveItems);

    category.IsActive = false;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<CatalogCategory, Failure<CatalogCategoryAdministrationFailureReason>>.Success(category);
  }

  private Failure<CatalogCategoryAdministrationFailureReason>? Validate(string? name, string? colourHex, IReadOnlyCollection<CatalogCategory> categories, Guid? categoryBeingSaved)
  {
    if (string.IsNullOrWhiteSpace(name))
      return new() { Reason = CatalogCategoryAdministrationFailureReason.NameMissing };

    if (!_colour.IsWellFormed(colourHex))
      return new() { Reason = CatalogCategoryAdministrationFailureReason.ColourInvalid };

    var wantedName = name.Trim();

    var taken = categories.Any(category => category.Id != categoryBeingSaved && string.Equals(category.Name, wantedName, StringComparison.OrdinalIgnoreCase));

    if (taken)
      return new() { Reason = CatalogCategoryAdministrationFailureReason.NameTaken };

    return null;
  }

  private Result<TValue, Failure<CatalogCategoryAdministrationFailureReason>> Failed<TValue>(CatalogCategoryAdministrationFailureReason reason)
  {
    return Result<TValue, Failure<CatalogCategoryAdministrationFailureReason>>.Failed(new() { Reason = reason });
  }

  private async Task<Result<TValue, Failure<CatalogCategoryAdministrationFailureReason>>> RunAsync<TValue>(Func<CancellationToken, Task<Result<TValue, Failure<CatalogCategoryAdministrationFailureReason>>>> write,
                                                                                                           Func<Result<TValue, Failure<CatalogCategoryAdministrationFailureReason>>, bool> shouldCommit,
                                                                                                           CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<TValue, Failure<CatalogCategoryAdministrationFailureReason>> written = await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<TValue, Failure<CatalogCategoryAdministrationFailureReason>>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = shouldCommit(written)
                                                      };
                                             },
                                             cancellationToken);
  }
}
