namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed record ReleasedOrder(Guid Id, Guid FestivalId, int GlobalOrderNumber, string TableName, string CreatedAtUtc);
