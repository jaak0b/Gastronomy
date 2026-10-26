using GastronomyApp.Core.Entities;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed record PendingHandOut(OrderItem Item, Guid StationId, DateTime HandedOutAtUtc);
