namespace GastronomyApp.Desktop.Localization;

public interface IDesktopTextProvider
{
  public event Action? LanguageChanged;

  public void UseLanguage(string? languageCode);

  public string Get(string key);

  public string Format(string key, params TextPlaceholder[] placeholders);
}
