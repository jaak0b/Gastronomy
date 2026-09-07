namespace GastronomyApp.Core.Services;

public sealed class CatalogCategoryColour
{
  private const int HexDigitCount = 6;
  private const int WellFormedLength = HexDigitCount + 1;

  public bool IsWellFormed(string? colourHex)
  {
    if (colourHex is null || colourHex.Length != WellFormedLength || colourHex[0] != '#')
    {
      return false;
    }

    for (var position = 1; position < WellFormedLength; position++)
    {
      if (!Uri.IsHexDigit(colourHex[position]))
      {
        return false;
      }
    }

    return true;
  }
}
