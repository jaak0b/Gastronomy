namespace GastronomyApp.Core.Results;

public sealed record OpenWork(Guid StationId, Guid CatalogItemId, double ProductionMinutes, bool IsQueueIndependent);
