export interface CatalogItem {
  id: string
  name: string
  categoryId: string
  priceCents: number
  sortOrder: number
  isAvailable: boolean
  stationIds: string[]
  productionMinutes: number | null
  isQueueIndependent: boolean
}

export interface CatalogCategory {
  categoryId: string
  name: string
  colourHex: string
  sortOrder: number
}

export interface AdminCategory extends CatalogCategory {
  isActive: boolean
}

export interface AdminItemAtFestival {
  priceCents: number
  isAvailable: boolean
  stationIds: string[]
}

export interface AdminItem {
  itemId: string
  name: string
  categoryId: string
  sortOrder: number
  isActive: boolean
  productionMinutes: number | null
  isQueueIndependent: boolean
  atTheFestival: AdminItemAtFestival | null
}

export interface AdminCategoriesResponse {
  categories: AdminCategory[]
}

export interface AdminItemsResponse {
  items: AdminItem[]
}

export interface AdminFestival {
  festivalId: string
  name: string
  startsAtUtc: string
  endsAtUtc: string
  isHidden: boolean
  isRunning: boolean
  stationCount: number
  menuItemCount: number
  orderCount: number
}

export interface AdminFestivalsResponse {
  festivals: AdminFestival[]
}

export interface AdminStaffMember {
  staffMemberId: string
  name: string
  isActive: boolean
  hasDevice: boolean
  lastSeenAtUtc: string | null
  hasOutstandingInvitation: boolean
}

export interface AdminStaffMembersResponse {
  staffMembers: AdminStaffMember[]
}

export interface AdminStation {
  stationId: string
  name: string
  sortOrder: number
  isActive: boolean
  hasDevice: boolean
  isAtTheFestival: boolean
}

export interface AdminStationsResponse {
  stations: AdminStation[]
}

export interface CatalogStation {
  id: string
  name: string
  sortOrder: number
}

export interface RunningFestival {
  festivalId: string
  name: string
}

export interface Catalog {
  festival: RunningFestival | null
  categories: CatalogCategory[]
  items: CatalogItem[]
  stations: CatalogStation[]
}

export type DeliveryMode = 'together' | 'asItComes'

export type DeviceKind = 'staffMember' | 'station'

export interface DraftLine {
  catalogItemId: string
  note: string | null
  stationId: string | null
  name: string
  stationName: string
}

export interface DraftOrder {
  festivalId: string | null
  tableName: string
  lines: DraftLine[]
  clientOrderId: string | null
  deliveryModes: Record<string, DeliveryMode>
}

export interface OrderSubmitItem {
  catalogItemId: string
  unitPriceCents: number
  note: string | null
  stationId: string | null
  settlement: OrderSettlementLine | null
}

export interface StationDeliveryMode {
  stationId: string
  deliveryMode: DeliveryMode
}

export interface ConfirmedSettlement {
  amountPaidCents: number
  paymentNotice: string | null
}

export interface OrderSettlementLine {
  paidPriceCents: number
  paymentNotice: string | null
}

export interface OpenItemsSettleLine {
  orderItemId: string
  paidPriceCents: number
  paymentNotice: string | null
}

export interface OpenItemsSettleRequest {
  lines: OpenItemsSettleLine[]
}

export interface OrderSubmitRequest {
  clientOrderId: string
  tableName: string
  items: OrderSubmitItem[]
  deliveryModes: StationDeliveryMode[]
}

export interface OpenOrderItem {
  orderItemId: string
  orderId: string
  globalOrderNumber: number
  itemName: string
  note: string | null
  unitPriceCents: number
  orderedAtUtc: string
}

export interface OpenTable {
  tableName: string
  openAmountCents: number
  items: OpenOrderItem[]
}

export interface OpenItemsResponse {
  tables: OpenTable[]
  itemsWithoutAnOrderCount: number
}

export interface TableOrderRecordItem extends OpenOrderItem {
  fulfilledAtUtc: string | null
  settledAtUtc: string | null
}

export interface TableOrderRecord {
  orderId: string
  globalOrderNumber: number
  createdAtUtc: string
  staffMemberName: string
  items: TableOrderRecordItem[]
}

export interface TableOrderReport {
  tableName: string
  openAmountCents: number
  orders: TableOrderRecord[]
}

export interface TableNamesResponse {
  tableNames: string[]
}

export interface SettlementResponse {
  settledOrderItemIds: string[]
  reappliedOrderItemIds: string[]
  alreadySettledByOthersOrderItemIds: string[]
  otherPhonesWereTold: boolean
}

export interface OrderSubmitResponse {
  orderId: string
  globalOrderNumber: number
  totalCents: number
  createdAtUtc: string
  stationOrders: {
    stationOrderId: string
    stationId: string
    stationName: string
    stationOrderNumber: number
    itemIds: string[]
  }[]
}

export interface StaffMember {
  id: string
  name: string
}

export interface StationIdentity {
  id: string
  name: string
}

export type AppLanguage = 'de' | 'en'

export interface LanguageResponse {
  language: AppLanguage
}

export interface SessionInfo {
  deviceId: string
  deviceKind: DeviceKind
  staffMember: StaffMember | null
  station: StationIdentity | null
  language: AppLanguage
}

export interface RedeemResponse {
  deviceId: string
  deviceToken: string
  deviceKind: DeviceKind
  staffMember: StaffMember | null
  station: StationIdentity | null
  language: AppLanguage
}

export interface Invitation {
  invitationId: string
  qrUrl: string
  expiresAtUtc: string
  staffMember: StaffMember | null
  station: StationIdentity | null
}

export interface StationEstimate {
  stationId: string
  queuedMinutes: number
}

export interface EstimatesResponse {
  stations: StationEstimate[]
}

export interface StationOrderItem {
  orderItemId: string
  itemName: string
  note: string | null
  fulfilledAtUtc: string | null
}

export interface StationOrder {
  stationOrderId: string
  globalOrderNumber: number
  stationOrderNumber: number
  tableName: string
  staffMemberName: string
  deliveryMode: DeliveryMode
  createdAtUtc: string
  isHiddenFromAsItComesQueue: boolean
  itemCount: number
  fulfilledItemCount: number
  items: StationOrderItem[]
}

export interface StationOrdersResponse {
  station: StationIdentity
  orders: StationOrder[]
  asItComes: StationOrder[]
}

export interface StationFulfilledResponse {
  stationOrders: StationOrder[]
}
