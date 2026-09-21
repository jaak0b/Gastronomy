using ErrorOr;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class CatalogCategoryAdministrationService
{
  private readonly IAfterCommitActions _afterCommitActions;
  private readonly ICatalogChangeAnnouncer _announcer;
  private readonly CatalogCategoryOrdering _ordering;
  private readonly ICatalogCategoryRepository _repository;
  private readonly ITransactionRunner _transactionRunner;

  public CatalogCategoryAdministrationService(ICatalogCategoryRepository repository, CatalogCategoryOrdering ordering, ICatalogChangeAnnouncer announcer, IAfterCommitActions afterCommitActions, ITransactionRunner transactionRunner)
  {
    _repository = repository;
    _ordering = ordering;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
    _transactionRunner = transactionRunner;
  }

  public Task<IReadOnlyList<CatalogCategory>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAllOrderedAsync(cancellationToken);
  }

  public Task<ErrorOr<CatalogCategory>> CreateAsync(string? name, string? colourHex, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => CreatedAsync(name, colourHex, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, transactionCancellationToken)), cancellationToken);
  }

  public Task<ErrorOr<CatalogCategory>> UpdateAsync(Guid categoryId, string? name, string? colourHex, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => UpdatedAsync(categoryId, name, colourHex, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, transactionCancellationToken)), cancellationToken);
  }

  public Task<ErrorOr<IReadOnlyList<CatalogCategory>>> MoveAsync(Guid categoryId, CategoryMoveDirection direction, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => MovedAsync(categoryId, direction, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, transactionCancellationToken)), cancellationToken);
  }

  public Task<ErrorOr<CatalogCategory>> ActivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => SwitchedOnAsync(categoryId, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, transactionCancellationToken)), cancellationToken);
  }

  public Task<ErrorOr<CatalogCategory>> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => SwitchedOffAsync(categoryId, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, transactionCancellationToken)), cancellationToken);
  }

  private async Task<ErrorOr<CatalogCategory>> CreatedAsync(string? name, string? colourHex, CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);

    if (NameTaken(name, categories, null))
      return Refusal.CatalogCategory.NameTaken(name!.Trim());

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

    return created;
  }

  private async Task<ErrorOr<CatalogCategory>> UpdatedAsync(Guid categoryId, string? name, string? colourHex, CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);
    var category = categories.FirstOrDefault(candidate => candidate.Id == categoryId);

    if (category is null)
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    if (NameTaken(name, categories, categoryId))
      return Refusal.CatalogCategory.NameTaken(name!.Trim());

    category.Name = name!.Trim();
    category.ColourHex = colourHex!;
    await _repository.SaveChangesAsync(cancellationToken);

    return category;
  }

  private async Task<ErrorOr<IReadOnlyList<CatalogCategory>>> MovedAsync(Guid categoryId, CategoryMoveDirection direction, CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);

    if (categories.All(candidate => candidate.Id != categoryId))
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    IReadOnlyList<CatalogCategory> reordered = _ordering.Move(categories, categoryId, direction);

    if (_ordering.IsNumberedInOrder(reordered))
      return reordered.ToErrorOr();

    _ordering.NumberInOrder(reordered);

    await _repository.SaveChangesAsync(cancellationToken);

    return reordered.ToErrorOr();
  }

  private async Task<ErrorOr<CatalogCategory>> SwitchedOnAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    var category = await _repository.FindByIdAsync(categoryId, cancellationToken);

    if (category is null)
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    category.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return category;
  }

  private async Task<ErrorOr<CatalogCategory>> SwitchedOffAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    var category = await _repository.FindByIdAsync(categoryId, cancellationToken);

    if (category is null)
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    if (await _repository.HoldsActiveItemsAsync(categoryId, cancellationToken))
      return Refusal.CatalogCategory.CategoryHoldsActiveItems(categoryId);

    category.IsActive = false;
    await _repository.SaveChangesAsync(cancellationToken);

    return category;
  }

  private bool NameTaken(string? name, IReadOnlyCollection<CatalogCategory> categories, Guid? categoryBeingSaved)
  {
    var wantedName = name!.Trim();

    return categories.Any(category => category.Id != categoryBeingSaved && string.Equals(category.Name, wantedName, StringComparison.OrdinalIgnoreCase));
  }
}
