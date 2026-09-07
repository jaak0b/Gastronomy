using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Services;

public sealed record CatalogCategoryPosition(Guid CategoryId, int SortOrder);

public sealed class CatalogCategoryOrdering
{
  private const int FirstSortOrder = 1;

  public int NextSortOrder(IReadOnlyCollection<int> takenSortOrders)
  {
    ArgumentNullException.ThrowIfNull(takenSortOrders);

    return takenSortOrders.Count == 0 ? FirstSortOrder : takenSortOrders.Max() + 1;
  }

  public IReadOnlyList<CatalogCategoryPosition> Move(IReadOnlyList<Guid> orderedCategoryIds,
                                                     Guid categoryId,
                                                     CategoryMoveDirection direction)
  {
    ArgumentNullException.ThrowIfNull(orderedCategoryIds);

    List<Guid> reordered = [.. orderedCategoryIds];
    var position = reordered.IndexOf(categoryId);
    var target = direction == CategoryMoveDirection.Up ? position - 1 : position + 1;

    if (position >= 0 && target >= 0 && target < reordered.Count)
    {
      var moving = reordered[position];
      reordered[position] = reordered[target];
      reordered[target] = moving;
    }

    return [.. reordered.Select((id, index) => new CatalogCategoryPosition(id, index + FirstSortOrder))];
  }
}
