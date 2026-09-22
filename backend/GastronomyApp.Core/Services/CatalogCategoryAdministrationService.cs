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

  public CatalogCategoryAdministrationService(ICatalogCategoryRepository repository, CatalogCategoryOrdering ordering, ICatalogChangeAnnouncer announcer, IAfterCommitActions afterCommitActions)
  {
    _repository = repository;
    _ordering = ordering;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
  }

  public Task<IReadOnlyList<CatalogCategory>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAllOrderedAsync(cancellationToken);
  }

  public async Task<ErrorOr<CatalogCategory>> CreateAsync(string? name, string? colourHex, CancellationToken cancellationToken)
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
    await _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, cancellationToken);

    return created;
  }

  public async Task<ErrorOr<CatalogCategory>> UpdateAsync(Guid categoryId, string? name, string? colourHex, CancellationToken cancellationToken)
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
    await _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, cancellationToken);

    return category;
  }

  public async Task<ErrorOr<IReadOnlyList<CatalogCategory>>> MoveAsync(Guid categoryId, CategoryMoveDirection direction, CancellationToken cancellationToken)
  {
    IReadOnlyList<CatalogCategory> categories = await _repository.FindAllOrderedAsync(cancellationToken);

    if (categories.All(candidate => candidate.Id != categoryId))
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    IReadOnlyList<CatalogCategory> reordered = _ordering.Move(categories, categoryId, direction);

    if (!_ordering.IsNumberedInOrder(reordered))
    {
      _ordering.NumberInOrder(reordered);
      await _repository.SaveChangesAsync(cancellationToken);
    }

    await _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, cancellationToken);

    return reordered.ToErrorOr();
  }

  public async Task<ErrorOr<CatalogCategory>> ActivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    var category = await _repository.FindByIdAsync(categoryId, cancellationToken);

    if (category is null)
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    category.IsActive = true;
    await _repository.SaveChangesAsync(cancellationToken);
    await _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, cancellationToken);

    return category;
  }

  public async Task<ErrorOr<CatalogCategory>> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
  {
    var category = await _repository.FindByIdAsync(categoryId, cancellationToken);

    if (category is null)
      return Refusal.CatalogCategory.CategoryNotFound(categoryId);

    if (await _repository.HoldsActiveItemsAsync(categoryId, cancellationToken))
      return Refusal.CatalogCategory.CategoryHoldsActiveItems(categoryId);

    category.IsActive = false;
    await _repository.SaveChangesAsync(cancellationToken);
    await _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceCatalogChangedAsync, cancellationToken);

    return category;
  }

  private bool NameTaken(string? name, IReadOnlyCollection<CatalogCategory> categories, Guid? categoryBeingSaved)
  {
    var wantedName = name!.Trim();

    return categories.Any(category => category.Id != categoryBeingSaved && string.Equals(category.Name, wantedName, StringComparison.OrdinalIgnoreCase));
  }
}
