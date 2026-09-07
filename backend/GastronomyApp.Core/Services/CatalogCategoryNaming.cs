namespace GastronomyApp.Core.Services;

public sealed class CatalogCategoryNaming
{
  public string Cleaned(string name)
  {
    ArgumentNullException.ThrowIfNull(name);

    return name.Trim();
  }

  public string Normalized(string name)
  {
    return Cleaned(name).ToUpperInvariant();
  }
}
