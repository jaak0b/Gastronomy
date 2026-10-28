using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.OpenItems;

public sealed record TableOrderRecordItemView(Guid OrderItemId, Guid OrderId, int GlobalOrderNumber, string ItemName, string? Note, int UnitPriceCents, OrderItemState State, DateTime StateSetAtUtc, bool WasHandedOutWhenCancelled, DateTime? SettledAtUtc, int? ChargedPriceCents);
