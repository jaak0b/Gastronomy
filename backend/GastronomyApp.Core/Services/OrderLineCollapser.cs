namespace GastronomyApp.Core.Services;

public sealed record CollapsedOrderLine<TLine>(TLine Line, int Quantity);

public sealed class OrderLineCollapser
{
  public IReadOnlyList<CollapsedOrderLine<TLine>> Collapse<TLine>(
      IReadOnlyList<TLine> lines,
      Func<TLine, string> itemNameOf,
      Func<TLine, string?> noteOf)
  {
    Dictionary<LineKey, int> countByKey = [];
    List<LineKey> order = [];
    Dictionary<LineKey, TLine> firstLineByKey = [];

    foreach (TLine line in lines)
    {
      LineKey key = new(itemNameOf(line), noteOf(line) ?? string.Empty);
      if (countByKey.TryGetValue(key, out int seen))
      {
        countByKey[key] = seen + 1;
        continue;
      }

      countByKey.Add(key, 1);
      firstLineByKey.Add(key, line);
      order.Add(key);
    }

    return [.. order.Select(key => new CollapsedOrderLine<TLine>(firstLineByKey[key], countByKey[key]))];
  }

  private sealed record LineKey(string ItemName, string Note);
}
