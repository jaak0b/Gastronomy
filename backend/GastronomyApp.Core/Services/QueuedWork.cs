namespace GastronomyApp.Core.Services;

public sealed record QueuedWork(double? ProductionMinutes, bool IsQueueIndependent);
