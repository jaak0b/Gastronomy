// @ts-nocheck
import type * as __TypedOpenapi from "./generatedSchemas.types.js";

  import { z } from "zod";

// <Schemas>
export type AdminCategoryView = __TypedOpenapi.Schemas.AdminCategoryView;
export const AdminCategoryView = z.strictObject({ categoryId: z.string(), name: z.string(), colourHex: z.string(), sortOrder: z.number().int(), isActive: z.boolean() });

export type AdminCategoryListView = __TypedOpenapi.Schemas.AdminCategoryListView;
export const AdminCategoryListView = z.strictObject({ categories: z.array(AdminCategoryView) });

export type AdminFestivalView = __TypedOpenapi.Schemas.AdminFestivalView;
export const AdminFestivalView = z.strictObject({ festivalId: z.string(), name: z.string(), startsAtUtc: z.string(), endsAtUtc: z.string(), isHidden: z.boolean(), isRunning: z.boolean(), stationCount: z.number().int(), menuItemCount: z.number().int(), orderCount: z.number().int() });

export type AdminFestivalListView = __TypedOpenapi.Schemas.AdminFestivalListView;
export const AdminFestivalListView = z.strictObject({ festivals: z.array(AdminFestivalView) });

export type AdminItemAtFestivalView = __TypedOpenapi.Schemas.AdminItemAtFestivalView;
export const AdminItemAtFestivalView = z.strictObject({ priceCents: z.number().int(), isAvailable: z.boolean(), stationIds: z.array(z.string()) });

export type AdminItemView = __TypedOpenapi.Schemas.AdminItemView;
export const AdminItemView = z.strictObject({ itemId: z.string(), name: z.string(), categoryId: z.string(), sortOrder: z.number().int(), isActive: z.boolean(), productionMinutes: z.number().nullable(), isQueueIndependent: z.boolean(), atTheFestival: AdminItemAtFestivalView.nullable() });

export type AdminItemListView = __TypedOpenapi.Schemas.AdminItemListView;
export const AdminItemListView = z.strictObject({ items: z.array(AdminItemView) });

export type AdminStaffMemberView = __TypedOpenapi.Schemas.AdminStaffMemberView;
export const AdminStaffMemberView = z.strictObject({ staffMemberId: z.string(), name: z.string(), isActive: z.boolean(), hasDevice: z.boolean(), lastSeenAtUtc: z.string().nullable(), hasOutstandingInvitation: z.boolean() });

export type AdminStaffMemberListView = __TypedOpenapi.Schemas.AdminStaffMemberListView;
export const AdminStaffMemberListView = z.strictObject({ staffMembers: z.array(AdminStaffMemberView) });

export type AdminStationView = __TypedOpenapi.Schemas.AdminStationView;
export const AdminStationView = z.strictObject({ stationId: z.string(), name: z.string(), sortOrder: z.number().int(), isActive: z.boolean(), hasDevice: z.boolean(), lastSeenAtUtc: z.string().nullable(), hasOutstandingInvitation: z.boolean(), isAtAnyFestival: z.boolean() });

export type AdminStationListView = __TypedOpenapi.Schemas.AdminStationListView;
export const AdminStationListView = z.strictObject({ stations: z.array(AdminStationView) });

export type CatalogCategoryView = __TypedOpenapi.Schemas.CatalogCategoryView;
export const CatalogCategoryView = z.strictObject({ categoryId: z.string(), name: z.string(), colourHex: z.string(), sortOrder: z.number().int() });

export type CatalogChangedEvent = __TypedOpenapi.Schemas.CatalogChangedEvent;
export const CatalogChangedEvent = z.record(z.string(), z.unknown());

export type CatalogItemView = __TypedOpenapi.Schemas.CatalogItemView;
export const CatalogItemView = z.strictObject({ id: z.string(), categoryId: z.string(), name: z.string(), priceCents: z.number().int(), sortOrder: z.number().int(), isAvailable: z.boolean(), productionMinutes: z.number().nullable(), isQueueIndependent: z.boolean(), stationIds: z.array(z.string()) });

export type CatalogStationView = __TypedOpenapi.Schemas.CatalogStationView;
export const CatalogStationView = z.strictObject({ id: z.string(), name: z.string(), sortOrder: z.number().int() });

export type RunningFestivalView = __TypedOpenapi.Schemas.RunningFestivalView;
export const RunningFestivalView = z.strictObject({ festivalId: z.string(), name: z.string() });

export type CatalogView = __TypedOpenapi.Schemas.CatalogView;
export const CatalogView = z.strictObject({ festival: RunningFestivalView.nullable(), categories: z.array(CatalogCategoryView), items: z.array(CatalogItemView), stations: z.array(CatalogStationView) });

export type CategoryMoveDirection = __TypedOpenapi.Schemas.CategoryMoveDirection;
export const CategoryMoveDirection = z.enum(["up", "down"]);

export type CreateInvitationRequest = __TypedOpenapi.Schemas.CreateInvitationRequest;
export const CreateInvitationRequest = z.strictObject({ staffMemberId: z.string().nullable(), stationId: z.string().nullable() }).partial();

export type DeliveryMode = __TypedOpenapi.Schemas.DeliveryMode;
export const DeliveryMode = z.enum(["together", "asItComes"]);

export type DeviceRevokedEvent = __TypedOpenapi.Schemas.DeviceRevokedEvent;
export const DeviceRevokedEvent = z.strictObject({ deviceId: z.string() });

export type EnrolmentCompletedEvent = __TypedOpenapi.Schemas.EnrolmentCompletedEvent;
export const EnrolmentCompletedEvent = z.strictObject({ staffMemberId: z.string().nullable(), stationId: z.string().nullable(), ownerName: z.string(), deviceId: z.string() });

export type FestivalChangedEvent = __TypedOpenapi.Schemas.FestivalChangedEvent;
export const FestivalChangedEvent = z.record(z.string(), z.unknown());

export type StaffMemberView = __TypedOpenapi.Schemas.StaffMemberView;
export const StaffMemberView = z.strictObject({ id: z.string(), name: z.string() });

export type StationSummaryView = __TypedOpenapi.Schemas.StationSummaryView;
export const StationSummaryView = z.strictObject({ id: z.string(), name: z.string() });

export type InvitationView = __TypedOpenapi.Schemas.InvitationView;
export const InvitationView = z.strictObject({ invitationId: z.string(), qrUrl: z.string(), expiresAtUtc: z.string(), staffMember: StaffMemberView.nullable(), station: StationSummaryView.nullable(), availableAddresses: z.array(z.string()) });

export type LanguageChangeRequest = __TypedOpenapi.Schemas.LanguageChangeRequest;
export const LanguageChangeRequest = z.strictObject({ language: z.string().nullable() });

export type LanguageView = __TypedOpenapi.Schemas.LanguageView;
export const LanguageView = z.strictObject({ language: z.string() });

export type MoveCategoryRequest = __TypedOpenapi.Schemas.MoveCategoryRequest;
export const MoveCategoryRequest = z.strictObject({ direction: CategoryMoveDirection });

export type OpenOrderItemView = __TypedOpenapi.Schemas.OpenOrderItemView;
export const OpenOrderItemView = z.strictObject({ orderItemId: z.string(), orderId: z.string(), globalOrderNumber: z.number().int(), itemName: z.string(), note: z.string().nullable(), unitPriceCents: z.number().int(), orderedAtUtc: z.string() });

export type OpenTableView = __TypedOpenapi.Schemas.OpenTableView;
export const OpenTableView = z.strictObject({ tableName: z.string(), openAmountCents: z.number().int(), items: z.array(OpenOrderItemView) });

export type OpenItemsView = __TypedOpenapi.Schemas.OpenItemsView;
export const OpenItemsView = z.strictObject({ tables: z.array(OpenTableView), itemsWithoutAnOrderCount: z.number().int() });

export type OrderDeliveryModeRequest = __TypedOpenapi.Schemas.OrderDeliveryModeRequest;
export const OrderDeliveryModeRequest = z.strictObject({ stationId: z.string(), deliveryMode: DeliveryMode });

export type OrderSettlementLineRequest = __TypedOpenapi.Schemas.OrderSettlementLineRequest;
export const OrderSettlementLineRequest = z.strictObject({ paidPriceCents: z.number().int().nullable(), paymentNotice: z.string().nullable().optional() });

export type OrderItemRequest = __TypedOpenapi.Schemas.OrderItemRequest;
export const OrderItemRequest = z.strictObject({ catalogItemId: z.string(), unitPriceCents: z.number().int(), note: z.string().nullable().optional(), stationId: z.string().nullable().optional(), settlement: OrderSettlementLineRequest.nullable().optional() });

export type OrderItemsSettledEvent = __TypedOpenapi.Schemas.OrderItemsSettledEvent;
export const OrderItemsSettledEvent = z.strictObject({ orderItemIds: z.array(z.string()), tableNames: z.array(z.string()) });

export type OrderStatus = __TypedOpenapi.Schemas.OrderStatus;
export const OrderStatus = z.enum(["open", "partiallyFulfilled", "fulfilled"]);

export type OrderStatusChangedEvent = __TypedOpenapi.Schemas.OrderStatusChangedEvent;
export const OrderStatusChangedEvent = z.strictObject({ orderId: z.string(), status: z.enum(["open", "partiallyFulfilled", "fulfilled"]) });

export type StationOrderView = __TypedOpenapi.Schemas.StationOrderView;
export const StationOrderView = z.strictObject({ stationOrderId: z.string(), stationId: z.string(), stationName: z.string(), stationOrderNumber: z.number().int(), deliveryMode: DeliveryMode, itemIds: z.array(z.string()) });

export type PlacedOrderView = __TypedOpenapi.Schemas.PlacedOrderView;
export const PlacedOrderView = z.strictObject({ orderId: z.string(), globalOrderNumber: z.number().int(), status: OrderStatus, totalCents: z.number().int(), createdAtUtc: z.string(), stationOrders: z.array(StationOrderView) });

export type PlaceOrderRequest = __TypedOpenapi.Schemas.PlaceOrderRequest;
export const PlaceOrderRequest = z.strictObject({ clientOrderId: z.string(), tableName: z.string().nullable(), items: z.array(OrderItemRequest).nullable(), deliveryModes: z.array(OrderDeliveryModeRequest).nullable().optional() });

export type RedeemedEnrolmentView = __TypedOpenapi.Schemas.RedeemedEnrolmentView;
export const RedeemedEnrolmentView = z.strictObject({ deviceId: z.string(), deviceToken: z.string(), staffMember: StaffMemberView.nullable(), station: StationSummaryView.nullable(), language: z.string() });

export type RedeemEnrolmentRequest = __TypedOpenapi.Schemas.RedeemEnrolmentRequest;
export const RedeemEnrolmentRequest = z.strictObject({ code: z.string().nullable(), name: z.string().nullable().optional(), userAgent: z.string().nullable().optional(), previousDeviceToken: z.string().nullable().optional() });

export type RenameStaffMemberRequest = __TypedOpenapi.Schemas.RenameStaffMemberRequest;
export const RenameStaffMemberRequest = z.strictObject({ name: z.string().nullable() });

export type SaveCategoryRequest = __TypedOpenapi.Schemas.SaveCategoryRequest;
export const SaveCategoryRequest = z.strictObject({ name: z.string().nullable(), colourHex: z.string().nullable() });

export type SavedFestivalView = __TypedOpenapi.Schemas.SavedFestivalView;
export const SavedFestivalView = z.strictObject({ festivalId: z.string() });

export type SavedItemView = __TypedOpenapi.Schemas.SavedItemView;
export const SavedItemView = z.strictObject({ itemId: z.string() });

export type SavedStationView = __TypedOpenapi.Schemas.SavedStationView;
export const SavedStationView = z.strictObject({ stationId: z.string() });

export type SaveFestivalItemRequest = __TypedOpenapi.Schemas.SaveFestivalItemRequest;
export const SaveFestivalItemRequest = z.strictObject({ priceCents: z.number().int(), stationIds: z.array(z.string()).nullable() });

export type SaveFestivalRequest = __TypedOpenapi.Schemas.SaveFestivalRequest;
export const SaveFestivalRequest = z.strictObject({ name: z.string().nullable(), startsAtUtc: z.string(), endsAtUtc: z.string() });

export type SaveItemRequest = __TypedOpenapi.Schemas.SaveItemRequest;
export const SaveItemRequest = z.strictObject({ name: z.string().nullable(), categoryId: z.string().nullable(), sortOrder: z.number().int(), productionMinutes: z.number().nullable().optional(), isQueueIndependent: z.boolean().optional() });

export type SaveStationRequest = __TypedOpenapi.Schemas.SaveStationRequest;
export const SaveStationRequest = z.strictObject({ name: z.string().nullable(), sortOrder: z.number().int() });

export type SessionView = __TypedOpenapi.Schemas.SessionView;
export const SessionView = z.strictObject({ deviceId: z.string(), staffMember: StaffMemberView.nullable(), station: StationSummaryView.nullable(), language: z.string() });

export type SetAvailabilityRequest = __TypedOpenapi.Schemas.SetAvailabilityRequest;
export const SetAvailabilityRequest = z.strictObject({ isAvailable: z.boolean() });

export type SettleLineRequest = __TypedOpenapi.Schemas.SettleLineRequest;
export const SettleLineRequest = z.strictObject({ orderItemId: z.string(), paidPriceCents: z.number().int().nullable(), paymentNotice: z.string().nullable().optional() });

export type SettleItemsRequest = __TypedOpenapi.Schemas.SettleItemsRequest;
export const SettleItemsRequest = z.strictObject({ lines: z.array(SettleLineRequest).nullable() });

export type SettlementView = __TypedOpenapi.Schemas.SettlementView;
export const SettlementView = z.strictObject({ settledOrderItemIds: z.array(z.string()), reappliedOrderItemIds: z.array(z.string()), alreadySettledByOthersOrderItemIds: z.array(z.string()) });

export type StationEstimateView = __TypedOpenapi.Schemas.StationEstimateView;
export const StationEstimateView = z.strictObject({ stationId: z.string(), queuedMinutes: z.number() });

export type StationEstimateListView = __TypedOpenapi.Schemas.StationEstimateListView;
export const StationEstimateListView = z.strictObject({ stations: z.array(StationEstimateView) });

export type StationQueueItemView = __TypedOpenapi.Schemas.StationQueueItemView;
export const StationQueueItemView = z.strictObject({ orderItemId: z.string(), itemName: z.string(), note: z.string().nullable(), fulfilledAtUtc: z.string().nullable() });

export type StationOrderQueueView = __TypedOpenapi.Schemas.StationOrderQueueView;
export const StationOrderQueueView = z.strictObject({ stationOrderId: z.string(), globalOrderNumber: z.number().int(), stationOrderNumber: z.number().int(), tableName: z.string(), staffMemberName: z.string(), deliveryMode: DeliveryMode, createdAtUtc: z.string(), isHiddenFromAsItComesQueue: z.boolean(), itemCount: z.number().int(), fulfilledItemCount: z.number().int(), items: z.array(StationQueueItemView) });

export type StationFulfilledView = __TypedOpenapi.Schemas.StationFulfilledView;
export const StationFulfilledView = z.strictObject({ stationOrders: z.array(StationOrderQueueView) });

export type StationItemSelectionRequest = __TypedOpenapi.Schemas.StationItemSelectionRequest;
export const StationItemSelectionRequest = z.strictObject({ orderItemIds: z.array(z.string()).nullable() });

export type StationOrdersChangedEvent = __TypedOpenapi.Schemas.StationOrdersChangedEvent;
export const StationOrdersChangedEvent = z.strictObject({ stationId: z.string() });

export type StationQueueView = __TypedOpenapi.Schemas.StationQueueView;
export const StationQueueView = z.strictObject({ station: StationSummaryView, orders: z.array(StationOrderQueueView), asItComes: z.array(StationOrderQueueView) });

export type StationsChangedEvent = __TypedOpenapi.Schemas.StationsChangedEvent;
export const StationsChangedEvent = z.record(z.string(), z.unknown());

export type TableNamesView = __TypedOpenapi.Schemas.TableNamesView;
export const TableNamesView = z.strictObject({ tableNames: z.array(z.string()) });

export type TableOrderRecordItemView = __TypedOpenapi.Schemas.TableOrderRecordItemView;
export const TableOrderRecordItemView = z.strictObject({ orderItemId: z.string(), orderId: z.string(), globalOrderNumber: z.number().int(), itemName: z.string(), note: z.string().nullable(), unitPriceCents: z.number().int(), orderedAtUtc: z.string(), fulfilledAtUtc: z.string().nullable(), settledAtUtc: z.string().nullable() });

export type TableOrderRecordView = __TypedOpenapi.Schemas.TableOrderRecordView;
export const TableOrderRecordView = z.strictObject({ orderId: z.string(), globalOrderNumber: z.number().int(), createdAtUtc: z.string(), staffMemberName: z.string(), items: z.array(TableOrderRecordItemView) });

export type TableOrderReportView = __TypedOpenapi.Schemas.TableOrderReportView;
export const TableOrderReportView = z.strictObject({ tableName: z.string(), openAmountCents: z.number().int(), orders: z.array(TableOrderRecordView) });

// </Schemas>

  
  
  