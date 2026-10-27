using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.OpenItems;

public sealed record TableOrderRecordItemView(Guid OrderItemId, Guid OrderId, int GlobalOrderNumber, string ItemName, string? Note, int UnitPriceCents, DateTime OrderedAtUtc, OrderItemState State, DateTime StateSetAtUtc, DateTime? SettledAtUtc, int? ChargedPriceCents);
