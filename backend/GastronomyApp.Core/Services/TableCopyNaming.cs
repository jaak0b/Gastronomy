using System.Globalization;
using System.Numerics;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class TableCopyNaming
{
  public const int LongestTableName = 40;

  public IReadOnlyList<TableCopyName> NamesFor(IReadOnlyList<Table> sources, IReadOnlyCollection<string> takenNames)
  {
    ArgumentNullException.ThrowIfNull(sources);
    ArgumentNullException.ThrowIfNull(takenNames);

    HashSet<string> taken = new(takenNames, StringComparer.OrdinalIgnoreCase);
    List<TableCopyName> names = [];

    foreach (var source in sources.OrderBy(table => table.Name, StringComparer.OrdinalIgnoreCase).ThenBy(table => table.Id))
    {
      var name = FirstFreeNameAfter(source.Name, taken);
      taken.Add(name);
      names.Add(new(source.Id, name));
    }

    return names;
  }

  private string FirstFreeNameAfter(string sourceName, HashSet<string> takenNames)
  {
    var digitStart = sourceName.Length;

    while (digitStart > 0 && char.IsAsciiDigit(sourceName[digitStart - 1]))
      digitStart--;

    if (digitStart == sourceName.Length)
      return FirstFreeAppendedNumber(sourceName, takenNames);

    var prefix = sourceName[..digitStart];
    var digitCount = sourceName.Length - digitStart;
    var number = BigInteger.Parse(sourceName.AsSpan(digitStart), NumberStyles.None, CultureInfo.InvariantCulture) + BigInteger.One;

    while (true)
    {
      var candidate = prefix + number.ToString(CultureInfo.InvariantCulture).PadLeft(digitCount, '0');

      if (!takenNames.Contains(candidate))
        return candidate;

      number += BigInteger.One;
    }
  }

  private string FirstFreeAppendedNumber(string sourceName, HashSet<string> takenNames)
  {
    var number = 1;

    while (true)
    {
      var candidate = $"{sourceName} {number}";

      if (!takenNames.Contains(candidate))
        return candidate;

      number++;
    }
  }
}
