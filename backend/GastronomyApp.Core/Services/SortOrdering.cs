using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Services;

public sealed class SortOrdering
{
  private const int FirstSortOrder = 1;

  public int NextSortOrder(IReadOnlyCollection<int> takenSortOrders)
  {
    ArgumentNullException.ThrowIfNull(takenSortOrders);

    if (takenSortOrders.Count == 0)
      return FirstSortOrder;

    return takenSortOrders.Max() + 1;
  }

  public IReadOnlyList<TSortable> Move<TSortable>(IReadOnlyList<TSortable> ordered, Guid movedId, MoveDirection direction) where TSortable : ISortable
  {
    return Move(ordered, movedId, direction, entry => true);
  }

  public IReadOnlyList<TSortable> Move<TSortable>(IReadOnlyList<TSortable> ordered, Guid movedId, MoveDirection direction, Func<TSortable, bool> canSwapWith) where TSortable : ISortable
  {
    ArgumentNullException.ThrowIfNull(ordered);
    ArgumentNullException.ThrowIfNull(canSwapWith);

    List<TSortable> reordered = ordered.ToList();
    var position = reordered.FindIndex(entry => entry.Id == movedId);

    if (position < 0)
      return reordered;

    var step = 1;

    if (direction == MoveDirection.Up)
      step = -1;

    var target = position + step;

    while (target >= 0 && target < reordered.Count && !canSwapWith(reordered[target]))
      target += step;

    if (target < 0 || target >= reordered.Count)
      return reordered;

    var moved = reordered[position];
    reordered[position] = reordered[target];
    reordered[target] = moved;

    return reordered;
  }

  public bool IsNumberedInOrder<TSortable>(IReadOnlyList<TSortable> ordered) where TSortable : ISortable
  {
    ArgumentNullException.ThrowIfNull(ordered);

    return ordered.Select((entry, index) => entry.SortOrder == index + FirstSortOrder).All(numbered => numbered);
  }

  public void NumberInOrder<TSortable>(IReadOnlyList<TSortable> ordered) where TSortable : ISortable
  {
    ArgumentNullException.ThrowIfNull(ordered);

    for (var index = 0; index < ordered.Count; index++)
    {
      ordered[index].SortOrder = index + FirstSortOrder;
    }
  }
}
