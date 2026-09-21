using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class CatalogCategoryOrdering
{
  private const int FirstSortOrder = 1;

  public int NextSortOrder(IReadOnlyCollection<int> takenSortOrders)
  {
    ArgumentNullException.ThrowIfNull(takenSortOrders);

    if (takenSortOrders.Count == 0)
      return FirstSortOrder;

    return takenSortOrders.Max() + 1;
  }

  public IReadOnlyList<CatalogCategory> Move(IReadOnlyList<CatalogCategory> orderedCategories, Guid categoryId, CategoryMoveDirection direction)
  {
    ArgumentNullException.ThrowIfNull(orderedCategories);

    List<CatalogCategory> reordered = orderedCategories.ToList();
    var position = reordered.FindIndex(category => category.Id == categoryId);
    var target = position + 1;

    if (direction == CategoryMoveDirection.Up)
      target = position - 1;

    if (position >= 0 && target >= 0 && target < reordered.Count)
      (reordered[position], reordered[target]) = (reordered[target], reordered[position]);

    return reordered;
  }

  public bool IsNumberedInOrder(IReadOnlyList<CatalogCategory> orderedCategories)
  {
    ArgumentNullException.ThrowIfNull(orderedCategories);

    return orderedCategories.Select((category, index) => category.SortOrder == index + FirstSortOrder).All(numbered => numbered);
  }

  public void NumberInOrder(IReadOnlyList<CatalogCategory> orderedCategories)
  {
    ArgumentNullException.ThrowIfNull(orderedCategories);

    for (var index = 0; index < orderedCategories.Count; index++)
    {
      orderedCategories[index].SortOrder = index + FirstSortOrder;
    }
  }
}
