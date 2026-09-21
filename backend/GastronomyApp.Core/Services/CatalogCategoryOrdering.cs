using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.ReadModels;

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

  public IReadOnlyList<CatalogCategoryPosition> Move(IReadOnlyList<Guid> orderedCategoryIds, Guid categoryId, CategoryMoveDirection direction)
  {
    ArgumentNullException.ThrowIfNull(orderedCategoryIds);

    List<Guid> reordered = orderedCategoryIds.ToList();
    var position = reordered.IndexOf(categoryId);
    var target = position + 1;

    if (direction == CategoryMoveDirection.Up)
      target = position - 1;

    if (position >= 0 && target >= 0 && target < reordered.Count)
      (reordered[position], reordered[target]) = (reordered[target], reordered[position]);

    return reordered.Select((id, index) => new CatalogCategoryPosition(id, index + FirstSortOrder)).ToList();
  }
}
