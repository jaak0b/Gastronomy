using System.Resources;
using GastronomyApp.Desktop.Services;

namespace GastronomyApp.Desktop.Localization;

public sealed class DesktopTextProvider : IDesktopTextProvider
{
    private readonly ResourceManager resourceManager;

    public DesktopTextProvider()
    {
        resourceManager = new ResourceManager(
            "GastronomyApp.Desktop.Localization.Strings",
            typeof(DesktopTextProvider).Assembly);
    }

    public string Get(string key)
    {
        string? value = resourceManager.GetString(key);
        if (value is null)
        {
            throw new MissingManifestResourceException($"Desktop string '{key}' is missing.");
        }

        return value;
    }

    public string Format(string key, params TextPlaceholder[] placeholders)
    {
        string value = Get(key);

        foreach (TextPlaceholder placeholder in placeholders)
        {
            value = value.Replace($"{{{placeholder.Name}}}", placeholder.Value, StringComparison.Ordinal);
        }

        return value;
    }
}
