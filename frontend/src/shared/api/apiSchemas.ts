import { z } from 'zod'
import type {
  AdminCategory,
  AdminCategoriesResponse,
  AdminFestival,
  AdminFestivalsResponse,
  AdminItem,
  AdminItemAtFestival,
  AdminItemsResponse,
  AdminStaffMember,
  AdminStaffMembersResponse,
  AdminStation,
  AdminStationsResponse,
  Catalog,
  CatalogCategory,
  CatalogItem,
  CatalogStation,
  EstimatesResponse,
  Invitation,
  LanguageResponse,
  OpenItemsResponse,
  OpenOrderItem,
  OpenTable,
  OrderSubmitResponse,
  RedeemResponse,
  RunningFestival,
  SessionInfo,
  SettlementResponse,
  StaffMember,
  StationEstimate,
  StationFulfilledResponse,
  StationIdentity,
  StationOrder,
  StationOrderItem,
  StationOrdersResponse,
  TableNamesResponse,
} from './apiTypes'

const appLanguageSchema = z.enum(['de', 'en'])

const deviceKindSchema = z.enum(['staffMember', 'station'])

export const deliveryModeSchema = z.enum(['together', 'asItComes'])

const staffMemberSchema: z.ZodType<StaffMember> = z.object({
  id: z.string(),
  name: z.string(),
})

const stationIdentitySchema: z.ZodType<StationIdentity> = z.object({
  id: z.string(),
  name: z.string(),
})

const runningFestivalSchema: z.ZodType<RunningFestival> = z.object({
  festivalId: z.string(),
  name: z.string(),
})

const catalogCategorySchema: z.ZodType<CatalogCategory> = z.object({
  categoryId: z.string(),
  name: z.string(),
  colourHex: z.string(),
  sortOrder: z.number(),
})

const catalogItemSchema: z.ZodType<CatalogItem> = z.object({
  id: z.string(),
  name: z.string(),
  categoryId: z.string(),
  priceCents: z.number(),
  sortOrder: z.number(),
  isAvailable: z.boolean(),
  stationIds: z.array(z.string()),
  productionMinutes: z.number().nullable(),
  isQueueIndependent: z.boolean(),
})

const catalogStationSchema: z.ZodType<CatalogStation> = z.object({
  id: z.string(),
  name: z.string(),
  sortOrder: z.number(),
})

export const catalogSchema: z.ZodType<Catalog> = z.object({
  festival: runningFestivalSchema.nullable(),
  categories: z.array(catalogCategorySchema),
  items: z.array(catalogItemSchema),
  stations: z.array(catalogStationSchema),
})

const stationEstimateSchema: z.ZodType<StationEstimate> = z.object({
  stationId: z.string(),
  queuedMinutes: z.number(),
})

export const estimatesSchema: z.ZodType<EstimatesResponse> = z.object({
  stations: z.array(stationEstimateSchema),
})

export const languageSchema: z.ZodType<LanguageResponse> = z.object({
  language: appLanguageSchema,
})

export const orderSubmitResponseSchema: z.ZodType<OrderSubmitResponse> = z.object({
  orderId: z.string(),
  globalOrderNumber: z.number(),
  totalCents: z.number(),
  createdAtUtc: z.string(),
  stationOrders: z.array(
    z.object({
      stationOrderId: z.string(),
      stationId: z.string(),
      stationName: z.string(),
      stationOrderNumber: z.number(),
      itemIds: z.array(z.string()),
    }),
  ),
})

const stationOrderItemSchema: z.ZodType<StationOrderItem> = z.object({
  orderItemId: z.string(),
  itemName: z.string(),
  note: z.string().nullable(),
  fulfilledAtUtc: z.string().nullable(),
})

const stationOrderSchema: z.ZodType<StationOrder> = z.object({
  stationOrderId: z.string(),
  globalOrderNumber: z.number(),
  stationOrderNumber: z.number(),
  tableName: z.string(),
  deliveryMode: deliveryModeSchema,
  createdAtUtc: z.string(),
  isHiddenFromAsItComesQueue: z.boolean(),
  itemCount: z.number(),
  fulfilledItemCount: z.number(),
  items: z.array(stationOrderItemSchema),
})

export const stationOrdersResponseSchema: z.ZodType<StationOrdersResponse> = z.object({
  station: stationIdentitySchema,
  orders: z.array(stationOrderSchema),
  asItComes: z.array(stationOrderSchema),
})

export const stationFulfilledResponseSchema: z.ZodType<StationFulfilledResponse> = z.object({
  stationOrders: z.array(stationOrderSchema),
})

export const sessionInfoSchema: z.ZodType<SessionInfo> = z.object({
  deviceId: z.string(),
  deviceKind: deviceKindSchema,
  staffMember: staffMemberSchema.nullable(),
  station: stationIdentitySchema.nullable(),
  language: appLanguageSchema,
})

export const redeemResponseSchema: z.ZodType<RedeemResponse> = z.object({
  deviceId: z.string(),
  deviceToken: z.string(),
  deviceKind: deviceKindSchema,
  staffMember: staffMemberSchema.nullable(),
  station: stationIdentitySchema.nullable(),
  language: appLanguageSchema,
})

const openOrderItemSchema: z.ZodType<OpenOrderItem> = z.object({
  orderItemId: z.string(),
  orderId: z.string(),
  globalOrderNumber: z.number(),
  itemName: z.string(),
  note: z.string().nullable(),
  unitPriceCents: z.number(),
  orderedAtUtc: z.string(),
})

const openTableSchema: z.ZodType<OpenTable> = z.object({
  tableName: z.string(),
  openAmountCents: z.number(),
  items: z.array(openOrderItemSchema),
})

export const openItemsResponseSchema: z.ZodType<OpenItemsResponse> = z.object({
  tables: z.array(openTableSchema),
  itemsWithoutAnOrderCount: z.number(),
})

export const tableNamesResponseSchema: z.ZodType<TableNamesResponse> = z.object({
  tableNames: z.array(z.string()),
})

export const settlementResponseSchema: z.ZodType<SettlementResponse> = z.object({
  settledOrderItemIds: z.array(z.string()),
  reappliedOrderItemIds: z.array(z.string()),
  alreadySettledByOthersOrderItemIds: z.array(z.string()),
  otherPhonesWereTold: z.boolean(),
})

export const invitationSchema: z.ZodType<Invitation> = z.object({
  invitationId: z.string(),
  qrUrl: z.string(),
  expiresAtUtc: z.string(),
  staffMember: staffMemberSchema.nullable(),
  station: stationIdentitySchema.nullable(),
})

export const adminCategorySchema: z.ZodType<AdminCategory> = z.object({
  categoryId: z.string(),
  name: z.string(),
  colourHex: z.string(),
  sortOrder: z.number(),
  isActive: z.boolean(),
})

export const adminCategoriesResponseSchema: z.ZodType<AdminCategoriesResponse> = z.object({
  categories: z.array(adminCategorySchema),
})

const adminItemAtFestivalSchema: z.ZodType<AdminItemAtFestival> = z.object({
  priceCents: z.number(),
  isAvailable: z.boolean(),
  stationIds: z.array(z.string()),
})

export const adminItemSchema: z.ZodType<AdminItem> = z.object({
  itemId: z.string(),
  name: z.string(),
  categoryId: z.string(),
  sortOrder: z.number(),
  isActive: z.boolean(),
  productionMinutes: z.number().nullable(),
  isQueueIndependent: z.boolean(),
  atTheFestival: adminItemAtFestivalSchema.nullable(),
})

export const adminItemsResponseSchema: z.ZodType<AdminItemsResponse> = z.object({
  items: z.array(adminItemSchema),
})

const adminFestivalSchema: z.ZodType<AdminFestival> = z.object({
  festivalId: z.string(),
  name: z.string(),
  startsAtUtc: z.string(),
  endsAtUtc: z.string(),
  isHidden: z.boolean(),
  isRunning: z.boolean(),
  stationCount: z.number(),
  menuItemCount: z.number(),
  orderCount: z.number(),
})

export const adminFestivalsResponseSchema: z.ZodType<AdminFestivalsResponse> = z.object({
  festivals: z.array(adminFestivalSchema),
})

const adminStaffMemberSchema: z.ZodType<AdminStaffMember> = z.object({
  staffMemberId: z.string(),
  name: z.string(),
  isActive: z.boolean(),
  hasDevice: z.boolean(),
  lastSeenAtUtc: z.string().nullable(),
  hasOutstandingInvitation: z.boolean(),
})

export const adminStaffMembersResponseSchema: z.ZodType<AdminStaffMembersResponse> = z.object({
  staffMembers: z.array(adminStaffMemberSchema),
})

export const adminStationSchema: z.ZodType<AdminStation> = z.object({
  stationId: z.string(),
  name: z.string(),
  sortOrder: z.number(),
  isActive: z.boolean(),
  hasDevice: z.boolean(),
  isAtTheFestival: z.boolean(),
})

export const adminStationsResponseSchema: z.ZodType<AdminStationsResponse> = z.object({
  stations: z.array(adminStationSchema),
})
