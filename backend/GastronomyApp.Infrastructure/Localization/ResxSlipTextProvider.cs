using System.Globalization;
using System.Resources;
using GastronomyApp.Core.Localization;

namespace GastronomyApp.Infrastructure.Localization;

public sealed class ResxSlipTextProvider : ISlipTextProvider
{
    private readonly ResourceManager resourceManager;

    public ResxSlipTextProvider()
    {
        resourceManager = new ResourceManager(
            "GastronomyApp.Infrastructure.Localization.SlipStrings",
            typeof(ResxSlipTextProvider).Assembly);
    }

    public SlipStrings GetStrings(string languageCode)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(languageCode);

        return new SlipStrings(
            Read("ReprintBanner", culture),
            Read("ReprintTimePrefix", culture),
            Read("SlipNumberPrefix", culture),
            Read("OrderNumberPrefix", culture),
            Read("TablePrefix", culture),
            Read("StaffMemberPrefix", culture),
            Read("NotePrefix", culture),
            Read("ItemsTotalPrefix", culture),
            Read("AlsoGoesToPrefix", culture),
            Read("ChosenStationWasPrefix", culture),
            Read("TestSlipHeader", culture));
    }

    private string Read(string key, CultureInfo culture)
    {
        string? value = resourceManager.GetString(key, culture);
        if (value is null)
        {
            throw new MissingManifestResourceException($"Slip string '{key}' is missing for culture '{culture.Name}'.");
        }

        return value;
    }
}
