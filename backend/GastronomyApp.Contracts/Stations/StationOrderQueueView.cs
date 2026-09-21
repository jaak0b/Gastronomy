using GastronomyApp.Contracts.Enums;

namespace GastronomyApp.Contracts.Stations;

public sealed record StationOrderQueueView(Guid StationOrderId,
                                           int GlobalOrderNumber,
                                           int StationOrderNumber,
                                           string TableName,
                                           string StaffMemberName,
                                           DeliveryMode DeliveryMode,
                                           DateTime CreatedAtUtc,
                                           bool IsHiddenFromAsItComesQueue,
                                           int ItemCount,
                                           int FulfilledItemCount,
                                           IReadOnlyList<StationQueueItemView> Items);
