namespace GastronomyApp.Core.Services;

public sealed class CatalogCategoryNaming
{
  public string ToCleanedName(string name)
  {
    ArgumentNullException.ThrowIfNull(name);

    return name.Trim();
  }

  public string ToNormalizedName(string name)
  {
    return ToCleanedName(name).ToUpperInvariant();
  }
}
