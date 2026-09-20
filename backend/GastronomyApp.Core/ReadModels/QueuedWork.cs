namespace GastronomyApp.Core.ReadModels;

public sealed record QueuedWork(double? ProductionMinutes, bool IsQueueIndependent);
