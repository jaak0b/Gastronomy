namespace GastronomyApp.Api.Options;

public sealed class AppLanguage
{
  private const string GermanCode = "de";

  private string _current = GermanCode;

  public string Current
  {
    get => _current;
    set => _current = string.IsNullOrWhiteSpace(value) ? GermanCode : value;
  }
}
