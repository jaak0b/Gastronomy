using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.OpenItems;

public sealed record OrderItemHistoryView(string OrderedByStaffMemberName, DateTime OrderedAtUtc, IReadOnlyList<OrderItemStatusChangeView> StatusChanges, DateTime? SettledAtUtc, int? ChargedPriceCents, PaymentMethod? PaymentMethod, string? SettledByStaffMemberName, string? PaymentNotice);
