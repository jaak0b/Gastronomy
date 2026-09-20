using GastronomyApp.Core.ReadModels;

namespace GastronomyApp.Core.Services;

public sealed class OrderLineCollapser
{
  public IReadOnlyList<CollapsedOrderLine<TLine>> Collapse<TLine>(IReadOnlyList<TLine> lines, Func<TLine, string> readItemName, Func<TLine, string?> readNote)
  {
    Dictionary<LineKey, int> countByKey = [];
    List<LineKey> order = [];
    Dictionary<LineKey, TLine> firstLineByKey = [];

    foreach (var line in lines)
    {
      LineKey key = new(readItemName(line), readNote(line) ?? string.Empty);
      if (countByKey.TryGetValue(key, out var seen))
      {
        countByKey[key] = seen + 1;
        continue;
      }

      countByKey.Add(key, 1);
      firstLineByKey.Add(key, line);
      order.Add(key);
    }

    return order.Select(key => new CollapsedOrderLine<TLine>(firstLineByKey[key], countByKey[key])).ToList();
  }

  private sealed record LineKey(string ItemName, string Note);
}
