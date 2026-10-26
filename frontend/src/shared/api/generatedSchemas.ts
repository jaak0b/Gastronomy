// @ts-nocheck
import type * as __TypedOpenapi from "./generatedSchemas.types.js";

  import { z } from "zod";

// <Schemas>
export type AreaShapeGeometry = __TypedOpenapi.Schemas.AreaShapeGeometry;
export const AreaShapeGeometry = z.strictObject({ x: z.number(), y: z.number(), width: z.number(), height: z.number(), rotation: z.number() });

export type AdminTableView = __TypedOpenapi.Schemas.AdminTableView;
export const AdminTableView = z.strictObject({ tableId: z.string(), name: z.string(), geometry: AreaShapeGeometry, isHidden: z.boolean(), hasItemsToHandOutOrSettleAtTheRunningFestival: z.boolean() });

export type AdminStationShapeView = __TypedOpenapi.Schemas.AdminStationShapeView;
export const AdminStationShapeView = z.strictObject({ stationId: z.string(), stationName: z.string(), geometry: AreaShapeGeometry });

export type AdminAreaView = __TypedOpenapi.Schemas.AdminAreaView;
export const AdminAreaView = z.strictObject({ areaId: z.string(), name: z.string(), isHidden: z.boolean(), hasItemsToHandOutOrSettleAtTheRunningFestival: z.boolean(), tables: z.array(AdminTableView), stationShapes: z.array(AdminStationShapeView) });

export type AdminCategoryView = __TypedOpenapi.Schemas.AdminCategoryView;
export const AdminCategoryView = z.strictObject({ categoryId: z.string(), name: z.string(), colourHex: z.string(), sortOrder: z.number().int(), isActive: z.boolean() });

export type AdminCategoryListView = __TypedOpenapi.Schemas.AdminCategoryListView;
export const AdminCategoryListView = z.strictObject({ categories: z.array(AdminCategoryView) });

export type IngredientUsagePoint = __TypedOpenapi.Schemas.IngredientUsagePoint;
export const IngredientUsagePoint = z.strictObject({ atUtc: z.string(), usedAmount: z.number() });

export type StockWarning = __TypedOpenapi.Schemas.StockWarning;
export const StockWarning = z.enum(["none", "runsOutSoon", "usedUp"]);

export type AdminFestivalIngredientView = __TypedOpenapi.Schemas.AdminFestivalIngredientView;
export const AdminFestivalIngredientView = z.strictObject({ ingredientId: z.string(), name: z.string(), unitId: z.string(), unitName: z.string(), availableAmount: z.number().nullable(), usedAmount: z.number(), usage: z.array(IngredientUsagePoint), runsOutAtUtcSinceStart: z.string().nullable(), runsOutAtUtcFromLastTwoHours: z.string().nullable(), stockWarning: StockWarning, runsOutSoonAtUtc: z.string().nullable() });

export type AdminFestivalIngredientListView = __TypedOpenapi.Schemas.AdminFestivalIngredientListView;
export const AdminFestivalIngredientListView = z.strictObject({ ingredients: z.array(AdminFestivalIngredientView) });

export type AdminFestivalView = __TypedOpenapi.Schemas.AdminFestivalView;
export const AdminFestivalView = z.strictObject({ festivalId: z.string(), name: z.string(), startsAtUtc: z.string(), endsAtUtc: z.string(), isHidden: z.boolean(), isRunning: z.boolean(), stationCount: z.number().int(), menuItemCount: z.number().int(), orderCount: z.number().int() });

export type AdminFestivalListView = __TypedOpenapi.Schemas.AdminFestivalListView;
export const AdminFestivalListView = z.strictObject({ festivals: z.array(AdminFestivalView) });

export type AdminIngredientView = __TypedOpenapi.Schemas.AdminIngredientView;
export const AdminIngredientView = z.strictObject({ ingredientId: z.string(), name: z.string(), unitId: z.string(), unitName: z.string(), isActive: z.boolean(), activeItemNames: z.array(z.string()) });

export type AdminIngredientListView = __TypedOpenapi.Schemas.AdminIngredientListView;
export const AdminIngredientListView = z.strictObject({ ingredients: z.array(AdminIngredientView) });

export type AdminItemAtFestivalView = __TypedOpenapi.Schemas.AdminItemAtFestivalView;
export const AdminItemAtFestivalView = z.strictObject({ priceCents: z.number().int(), isAvailable: z.boolean(), stationIds: z.array(z.string()) });

export type AdminItemIngredientView = __TypedOpenapi.Schemas.AdminItemIngredientView;
export const AdminItemIngredientView = z.strictObject({ ingredientId: z.string(), name: z.string(), unitId: z.string(), unitName: z.string(), amount: z.number() });

export type AdminItemView = __TypedOpenapi.Schemas.AdminItemView;
export const AdminItemView = z.strictObject({ itemId: z.string(), name: z.string(), categoryId: z.string(), sortOrder: z.number().int(), isActive: z.boolean(), productionMinutes: z.number().nullable(), isQueueIndependent: z.boolean(), atTheFestival: AdminItemAtFestivalView.nullable(), ingredients: z.array(AdminItemIngredientView) });

export type AdminItemListView = __TypedOpenapi.Schemas.AdminItemListView;
export const AdminItemListView = z.strictObject({ items: z.array(AdminItemView) });

export type AdminStaffMemberView = __TypedOpenapi.Schemas.AdminStaffMemberView;
export const AdminStaffMemberView = z.strictObject({ staffMemberId: z.string(), name: z.string(), isActive: z.boolean(), hasDevice: z.boolean() });

export type AdminStaffMemberListView = __TypedOpenapi.Schemas.AdminStaffMemberListView;
export const AdminStaffMemberListView = z.strictObject({ staffMembers: z.array(AdminStaffMemberView) });

export type AdminStationView = __TypedOpenapi.Schemas.AdminStationView;
export const AdminStationView = z.strictObject({ stationId: z.string(), name: z.string(), sortOrder: z.number().int(), isActive: z.boolean(), hasDevice: z.boolean(), isAtFestival: z.boolean(), hasItemsToHandOutAtTheRunningFestival: z.boolean(), strandedItemCountWhenSwitchedOff: z.number().int().nullable(), strandedItemCountWhenRemovedFromTheFestival: z.number().int().nullable() });

export type AdminStationListView = __TypedOpenapi.Schemas.AdminStationListView;
export const AdminStationListView = z.strictObject({ stations: z.array(AdminStationView) });

export type AdminTablePlanView = __TypedOpenapi.Schemas.AdminTablePlanView;
export const AdminTablePlanView = z.strictObject({ areas: z.array(AdminAreaView) });

export type AdminUnitView = __TypedOpenapi.Schemas.AdminUnitView;
export const AdminUnitView = z.strictObject({ unitId: z.string(), name: z.string(), isActive: z.boolean(), usedByIngredientNames: z.array(z.string()) });

export type AdminUnitListView = __TypedOpenapi.Schemas.AdminUnitListView;
export const AdminUnitListView = z.strictObject({ units: z.array(AdminUnitView) });

export type CatalogCategoryView = __TypedOpenapi.Schemas.CatalogCategoryView;
export const CatalogCategoryView = z.strictObject({ categoryId: z.string(), name: z.string(), colourHex: z.string(), sortOrder: z.number().int() });

export type CatalogItemView = __TypedOpenapi.Schemas.CatalogItemView;
export const CatalogItemView = z.strictObject({ id: z.string(), categoryId: z.string(), name: z.string(), priceCents: z.number().int(), sortOrder: z.number().int(), isAvailable: z.boolean(), productionMinutes: z.number().nullable(), isQueueIndependent: z.boolean(), stationIds: z.array(z.string()) });

export type CatalogStationView = __TypedOpenapi.Schemas.CatalogStationView;
export const CatalogStationView = z.strictObject({ id: z.string(), name: z.string(), sortOrder: z.number().int() });

export type RunningFestivalView = __TypedOpenapi.Schemas.RunningFestivalView;
export const RunningFestivalView = z.strictObject({ festivalId: z.string(), name: z.string() });

export type CatalogView = __TypedOpenapi.Schemas.CatalogView;
export const CatalogView = z.strictObject({ festival: RunningFestivalView.nullable(), categories: z.array(CatalogCategoryView), items: z.array(CatalogItemView), stations: z.array(CatalogStationView) });

export type CopiedTablesView = __TypedOpenapi.Schemas.CopiedTablesView;
export const CopiedTablesView = z.strictObject({ tableIds: z.array(z.string()) });

export type CopyTableRequest = __TypedOpenapi.Schemas.CopyTableRequest;
export const CopyTableRequest = z.strictObject({ sourceTableId: z.string(), geometry: AreaShapeGeometry.nullable() });

export type CopyTablesRequest = __TypedOpenapi.Schemas.CopyTablesRequest;
export const CopyTablesRequest = z.strictObject({ areaId: z.string(), tables: z.array(CopyTableRequest).nullable() });

export type CreateInvitationRequest = __TypedOpenapi.Schemas.CreateInvitationRequest;
export const CreateInvitationRequest = z.strictObject({ staffMemberId: z.string().nullable(), stationId: z.string().nullable() }).partial();

export type DeliveryMode = __TypedOpenapi.Schemas.DeliveryMode;
export const DeliveryMode = z.enum(["together", "asItComes"]);

export type DeviceRevokedEvent = __TypedOpenapi.Schemas.DeviceRevokedEvent;
export const DeviceRevokedEvent = z.strictObject({ deviceId: z.string() });

export type EnrolmentCompletedEvent = __TypedOpenapi.Schemas.EnrolmentCompletedEvent;
export const EnrolmentCompletedEvent = z.strictObject({ staffMemberId: z.string().nullable(), stationId: z.string().nullable(), ownerName: z.string(), deviceId: z.string() });

export type EstimateQuoteLine = __TypedOpenapi.Schemas.EstimateQuoteLine;
export const EstimateQuoteLine = z.strictObject({ catalogItemId: z.string(), stationId: z.string(), units: z.number().int() });

export type EstimateQuoteRequest = __TypedOpenapi.Schemas.EstimateQuoteRequest;
export const EstimateQuoteRequest = z.strictObject({ lines: z.array(EstimateQuoteLine) });

export type StationQuoteView = __TypedOpenapi.Schemas.StationQuoteView;
export const StationQuoteView = z.strictObject({ stationId: z.string(), readyInMinutes: z.number().nullable() });

export type EstimateQuoteView = __TypedOpenapi.Schemas.EstimateQuoteView;
export const EstimateQuoteView = z.strictObject({ stations: z.array(StationQuoteView) });

export type StaffMemberView = __TypedOpenapi.Schemas.StaffMemberView;
export const StaffMemberView = z.strictObject({ id: z.string(), name: z.string() });

export type StationSummaryView = __TypedOpenapi.Schemas.StationSummaryView;
export const StationSummaryView = z.strictObject({ id: z.string(), name: z.string() });

export type InvitationView = __TypedOpenapi.Schemas.InvitationView;
export const InvitationView = z.strictObject({ invitationId: z.string(), qrUrl: z.string(), expiresAtUtc: z.string(), staffMember: StaffMemberView.nullable(), station: StationSummaryView.nullable() });

export type ItemEstimateView = __TypedOpenapi.Schemas.ItemEstimateView;
export const ItemEstimateView = z.strictObject({ catalogItemId: z.string(), stationId: z.string(), readyInMinutes: z.number() });

export type LanguageChangeRequest = __TypedOpenapi.Schemas.LanguageChangeRequest;
export const LanguageChangeRequest = z.strictObject({ language: z.string().nullable() });

export type LanguageView = __TypedOpenapi.Schemas.LanguageView;
export const LanguageView = z.strictObject({ language: z.string() });

export type MoveDirection = __TypedOpenapi.Schemas.MoveDirection;
export const MoveDirection = z.enum(["up", "down"]);

export type MoveAreaRequest = __TypedOpenapi.Schemas.MoveAreaRequest;
export const MoveAreaRequest = z.strictObject({ direction: MoveDirection, showsHiddenAreas: z.boolean() });

export type MoveCategoryRequest = __TypedOpenapi.Schemas.MoveCategoryRequest;
export const MoveCategoryRequest = z.strictObject({ direction: MoveDirection });

export type MoveTableRequest = __TypedOpenapi.Schemas.MoveTableRequest;
export const MoveTableRequest = z.strictObject({ tableId: z.string(), geometry: AreaShapeGeometry.nullable() });

export type MoveTablesRequest = __TypedOpenapi.Schemas.MoveTablesRequest;
export const MoveTablesRequest = z.strictObject({ tables: z.array(MoveTableRequest).nullable() });

export type OrderDeliveryModeRequest = __TypedOpenapi.Schemas.OrderDeliveryModeRequest;
export const OrderDeliveryModeRequest = z.strictObject({ stationId: z.string(), deliveryMode: DeliveryMode });

export type OrderItemRequest = __TypedOpenapi.Schemas.OrderItemRequest;
export const OrderItemRequest = z.strictObject({ catalogItemId: z.string(), unitPriceCents: z.number().int(), note: z.string().nullable().optional(), stationId: z.string().nullable().optional() });

export type OrderStatus = __TypedOpenapi.Schemas.OrderStatus;
export const OrderStatus = z.enum(["open", "partiallyFulfilled", "fulfilled"]);

export type PaymentMethod = __TypedOpenapi.Schemas.PaymentMethod;
export const PaymentMethod = z.union([z.literal("none"), z.literal("cash"), z.literal("card"), z.literal(null)]);

export type StationOrderView = __TypedOpenapi.Schemas.StationOrderView;
export const StationOrderView = z.strictObject({ stationOrderId: z.string(), stationId: z.string(), stationName: z.string(), stationOrderNumber: z.number().int(), deliveryMode: DeliveryMode, itemIds: z.array(z.string()) });

export type PlacedOrderView = __TypedOpenapi.Schemas.PlacedOrderView;
export const PlacedOrderView = z.strictObject({ orderId: z.string(), globalOrderNumber: z.number().int(), status: OrderStatus, totalCents: z.number().int(), createdAtUtc: z.string(), stationOrders: z.array(StationOrderView) });

export type PlaceOrderRequest = __TypedOpenapi.Schemas.PlaceOrderRequest;
export const PlaceOrderRequest = z.strictObject({ clientOrderId: z.string(), tableId: z.string(), items: z.array(OrderItemRequest).nullable(), deliveryModes: z.array(OrderDeliveryModeRequest).nullable().optional() });

export type RedeemedEnrolmentView = __TypedOpenapi.Schemas.RedeemedEnrolmentView;
export const RedeemedEnrolmentView = z.strictObject({ deviceId: z.string(), deviceToken: z.string(), staffMember: StaffMemberView.nullable(), station: StationSummaryView.nullable(), language: z.string() });

export type RedeemEnrolmentRequest = __TypedOpenapi.Schemas.RedeemEnrolmentRequest;
export const RedeemEnrolmentRequest = z.strictObject({ code: z.string().nullable(), name: z.string().nullable().optional(), previousDeviceToken: z.string().nullable().optional() });

export type RenameStaffMemberRequest = __TypedOpenapi.Schemas.RenameStaffMemberRequest;
export const RenameStaffMemberRequest = z.strictObject({ name: z.string().nullable() });

export type SaveAreaRequest = __TypedOpenapi.Schemas.SaveAreaRequest;
export const SaveAreaRequest = z.strictObject({ name: z.string().nullable() });

export type SaveCategoryRequest = __TypedOpenapi.Schemas.SaveCategoryRequest;
export const SaveCategoryRequest = z.strictObject({ name: z.string().nullable(), colourHex: z.string().nullable() });

export type SavedAreaView = __TypedOpenapi.Schemas.SavedAreaView;
export const SavedAreaView = z.strictObject({ areaId: z.string() });

export type SavedFestivalIngredientView = __TypedOpenapi.Schemas.SavedFestivalIngredientView;
export const SavedFestivalIngredientView = z.strictObject({ ingredientId: z.string() });

export type SavedFestivalView = __TypedOpenapi.Schemas.SavedFestivalView;
export const SavedFestivalView = z.strictObject({ festivalId: z.string() });

export type SavedIngredientView = __TypedOpenapi.Schemas.SavedIngredientView;
export const SavedIngredientView = z.strictObject({ ingredientId: z.string() });

export type SavedItemView = __TypedOpenapi.Schemas.SavedItemView;
export const SavedItemView = z.strictObject({ itemId: z.string() });

export type SavedStationView = __TypedOpenapi.Schemas.SavedStationView;
export const SavedStationView = z.strictObject({ stationId: z.string() });

export type SavedTableView = __TypedOpenapi.Schemas.SavedTableView;
export const SavedTableView = z.strictObject({ tableId: z.string() });

export type SavedUnitView = __TypedOpenapi.Schemas.SavedUnitView;
export const SavedUnitView = z.strictObject({ unitId: z.string() });

export type SaveFestivalIngredientRequest = __TypedOpenapi.Schemas.SaveFestivalIngredientRequest;
export const SaveFestivalIngredientRequest = z.strictObject({ availableAmount: z.number().nullable() });

export type SaveFestivalItemRequest = __TypedOpenapi.Schemas.SaveFestivalItemRequest;
export const SaveFestivalItemRequest = z.strictObject({ priceCents: z.number().int(), stationIds: z.array(z.string()).nullable() });

export type SaveFestivalRequest = __TypedOpenapi.Schemas.SaveFestivalRequest;
export const SaveFestivalRequest = z.strictObject({ name: z.string().nullable(), startsAtUtc: z.string(), endsAtUtc: z.string() });

export type SaveIngredientRequest = __TypedOpenapi.Schemas.SaveIngredientRequest;
export const SaveIngredientRequest = z.strictObject({ name: z.string().nullable(), unitId: z.string().nullable() });

export type SaveItemIngredientRequest = __TypedOpenapi.Schemas.SaveItemIngredientRequest;
export const SaveItemIngredientRequest = z.strictObject({ amount: z.number().nullable() });

export type SaveItemRequest = __TypedOpenapi.Schemas.SaveItemRequest;
export const SaveItemRequest = z.strictObject({ name: z.string().nullable(), categoryId: z.string().nullable(), sortOrder: z.number().int(), productionMinutes: z.number().nullable().optional(), isQueueIndependent: z.boolean().optional() });

export type SaveStationRequest = __TypedOpenapi.Schemas.SaveStationRequest;
export const SaveStationRequest = z.strictObject({ name: z.string().nullable(), sortOrder: z.number().int() });

export type SaveStationShapeRequest = __TypedOpenapi.Schemas.SaveStationShapeRequest;
export const SaveStationShapeRequest = z.strictObject({ areaId: z.string(), geometry: AreaShapeGeometry.nullable() });

export type SaveTableRequest = __TypedOpenapi.Schemas.SaveTableRequest;
export const SaveTableRequest = z.strictObject({ areaId: z.string(), name: z.string().nullable(), geometry: AreaShapeGeometry.nullable() });

export type SaveUnitRequest = __TypedOpenapi.Schemas.SaveUnitRequest;
export const SaveUnitRequest = z.strictObject({ name: z.string().nullable() });

export type SessionView = __TypedOpenapi.Schemas.SessionView;
export const SessionView = z.strictObject({ deviceId: z.string(), staffMember: StaffMemberView.nullable(), station: StationSummaryView.nullable(), language: z.string() });

export type SetAvailabilityRequest = __TypedOpenapi.Schemas.SetAvailabilityRequest;
export const SetAvailabilityRequest = z.strictObject({ isAvailable: z.boolean() });

export type SettleLineRequest = __TypedOpenapi.Schemas.SettleLineRequest;
export const SettleLineRequest = z.strictObject({ orderItemId: z.string(), paidPriceCents: z.number().int().nullable(), paymentNotice: z.string().nullable().optional() });

export type SettleItemsRequest = __TypedOpenapi.Schemas.SettleItemsRequest;
export const SettleItemsRequest = z.strictObject({ lines: z.array(SettleLineRequest).nullable(), paymentMethod: PaymentMethod.nullable() });

export type SettlementView = __TypedOpenapi.Schemas.SettlementView;
export const SettlementView = z.strictObject({ settledOrderItemIds: z.array(z.string()), reappliedOrderItemIds: z.array(z.string()), alreadySettledByOthersOrderItemIds: z.array(z.string()) });

export type ShareState = __TypedOpenapi.Schemas.ShareState;
export const ShareState = z.enum(["none", "some", "all"]);

export type StationQueueItemView = __TypedOpenapi.Schemas.StationQueueItemView;
export const StationQueueItemView = z.strictObject({ orderItemId: z.string(), itemName: z.string(), note: z.string().nullable(), fulfilledAtUtc: z.string().nullable() });

export type StationOrderQueueView = __TypedOpenapi.Schemas.StationOrderQueueView;
export const StationOrderQueueView = z.strictObject({ stationOrderId: z.string(), globalOrderNumber: z.number().int(), stationOrderNumber: z.number().int(), tableName: z.string(), staffMemberName: z.string(), deliveryMode: DeliveryMode, createdAtUtc: z.string(), isHiddenFromAsItComesQueue: z.boolean(), itemCount: z.number().int(), fulfilledItemCount: z.number().int(), items: z.array(StationQueueItemView) });

export type StationFulfilledView = __TypedOpenapi.Schemas.StationFulfilledView;
export const StationFulfilledView = z.strictObject({ stationOrders: z.array(StationOrderQueueView) });

export type StationItemSelectionRequest = __TypedOpenapi.Schemas.StationItemSelectionRequest;
export const StationItemSelectionRequest = z.strictObject({ orderItemIds: z.array(z.string()).nullable() });

export type StationQueueView = __TypedOpenapi.Schemas.StationQueueView;
export const StationQueueView = z.strictObject({ station: StationSummaryView, orders: z.array(StationOrderQueueView), asItComes: z.array(StationOrderQueueView) });

export type TableOrderRecordItemView = __TypedOpenapi.Schemas.TableOrderRecordItemView;
export const TableOrderRecordItemView = z.strictObject({ orderItemId: z.string(), orderId: z.string(), globalOrderNumber: z.number().int(), itemName: z.string(), note: z.string().nullable(), unitPriceCents: z.number().int(), orderedAtUtc: z.string(), fulfilledAtUtc: z.string().nullable(), settledAtUtc: z.string().nullable() });

export type TableOrderRecordView = __TypedOpenapi.Schemas.TableOrderRecordView;
export const TableOrderRecordView = z.strictObject({ orderId: z.string(), globalOrderNumber: z.number().int(), createdAtUtc: z.string(), staffMemberName: z.string(), items: z.array(TableOrderRecordItemView) });

export type TableOrderReportView = __TypedOpenapi.Schemas.TableOrderReportView;
export const TableOrderReportView = z.strictObject({ tableId: z.string(), tableName: z.string(), openAmountCents: z.number().int(), orders: z.array(TableOrderRecordView) });

export type TablePlanTableView = __TypedOpenapi.Schemas.TablePlanTableView;
export const TablePlanTableView = z.strictObject({ tableId: z.string(), name: z.string(), geometry: AreaShapeGeometry, handOutProgress: ShareState, settlementProgress: ShareState });

export type TablePlanStationShapeView = __TypedOpenapi.Schemas.TablePlanStationShapeView;
export const TablePlanStationShapeView = z.strictObject({ stationName: z.string(), geometry: AreaShapeGeometry });

export type TablePlanAreaView = __TypedOpenapi.Schemas.TablePlanAreaView;
export const TablePlanAreaView = z.strictObject({ areaId: z.string(), name: z.string(), tables: z.array(TablePlanTableView), stationShapes: z.array(TablePlanStationShapeView) });

export type TablePlanView = __TypedOpenapi.Schemas.TablePlanView;
export const TablePlanView = z.strictObject({ areas: z.array(TablePlanAreaView) });

// </Schemas>

  
  
  