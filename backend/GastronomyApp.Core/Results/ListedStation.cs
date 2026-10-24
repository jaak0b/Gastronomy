using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record ListedStation(Station Station, bool HasItemsToHandOut);
