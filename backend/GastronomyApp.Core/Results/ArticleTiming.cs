namespace GastronomyApp.Core.Results;

public sealed record ArticleTiming(Guid CatalogItemId, double? ProductionMinutes, bool IsQueueIndependent);
