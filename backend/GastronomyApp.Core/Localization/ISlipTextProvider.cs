namespace GastronomyApp.Core.Localization;

public interface ISlipTextProvider
{
    public SlipStrings GetStrings(string languageCode);
}

public sealed record SlipStrings(
    string ReprintBanner,
    string ReprintTimePrefix,
    string SlipNumberPrefix,
    string OrderNumberPrefix,
    string TablePrefix,
    string ServerPrefix,
    string NotePrefix,
    string ItemsTotalPrefix,
    string AlsoGoesToPrefix,
    string ChosenStationWasPrefix,
    string TestSlipHeader,
    string StationCardInstructions);
