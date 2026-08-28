namespace GastronomyApp.Api.Options;

public sealed class AppLanguage
{
  private const string GermanCode = "de";

  private string current = GermanCode;

  public string Current
  {
    get => current;
    set => current = string.IsNullOrWhiteSpace(value) ? GermanCode : value;
  }
}
