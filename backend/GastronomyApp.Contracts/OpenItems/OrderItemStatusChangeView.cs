using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.OpenItems;

public sealed record OrderItemStatusChangeView(OrderItemState State, DateTime ChangedAtUtc, string? StaffMemberName, string? StationName);
