using GastronomyApp.Core.Enums;

namespace GastronomyApp.Api.Contracts;

public sealed record StationOrderView(Guid StationOrderId, Guid StationId, string StationName, int StationOrderNumber, DeliveryMode DeliveryMode, IReadOnlyList<Guid> ItemIds);
