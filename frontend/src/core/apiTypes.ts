export interface CatalogItem {
  id: string
  name: string
  categoryId: string
  priceCents: number
  sortOrder: number
  isAvailable: boolean
  stationIds: string[]
  productionMinutes: number | null
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

export interface CatalogStation {
  id: string
  name: string
  sortOrder: number
}

export interface Catalog {
  categories: CatalogCategory[]
  items: CatalogItem[]
  stations: CatalogStation[]
}

export type DeliveryMode = 'together' | 'asItComes'

export type ProductionStatus = 'waiting' | 'inProduction' | 'finished'

export type DeviceKind = 'staffMember' | 'station'

export interface DraftLine {
  catalogItemId: string
  note: string | null
  stationId: string | null
  name: string
  unitPriceCents: number
}

export interface DraftOrder {
  tableName: string
  note: string | null
  lines: DraftLine[]
  clientOrderId: string | null
  deliveryModes: Record<string, DeliveryMode>
}

export interface OrderSubmitItem {
  catalogItemId: string
  unitPriceCents: number
  note: string | null
  stationId: string | null
}

export interface StationDeliveryMode {
  stationId: string
  deliveryMode: DeliveryMode
}

export interface OrderSubmitRequest {
  clientOrderId: string
  tableName: string
  note: string | null
  settleOnSend: boolean
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
  stationName: string
  deliveryMode: DeliveryMode
  productionStatus: ProductionStatus
}

export interface GivenAwayOrderItem {
  orderItemId: string
  orderId: string
  globalOrderNumber: number
  itemName: string
  waivedAmountCents: number
  paymentNotice: string | null
  settledAtUtc: string
}

export interface OpenTable {
  tableName: string
  openAmountCents: number
  givenAwayAmountCents: number
  items: OpenOrderItem[]
  givenAwayItems: GivenAwayOrderItem[]
}

export interface OpenItemsResponse {
  tables: OpenTable[]
  itemsWithoutAnOrderCount: number
}

export interface TableNamesResponse {
  tableNames: string[]
}

export interface SettlementResponse {
  settledOrderItemIds: string[]
  alreadySettledOrderItemIds: string[]
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

export interface SessionInfo {
  deviceId: string
  deviceKind?: DeviceKind
  staffMember: StaffMember | null
  station?: StationIdentity | null
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

export interface StationSliceItem {
  orderItemId: string
  itemName: string
  note: string | null
  productionStatus: ProductionStatus
}

export interface StationSlice {
  stationOrderId: string
  globalOrderNumber: number
  stationOrderNumber: number
  tableName: string
  note: string | null
  deliveryMode: DeliveryMode
  createdAtUtc: string
  items: StationSliceItem[]
}

export interface StationOrdersResponse {
  station: StationIdentity
  slices: StationSlice[]
}

export interface StationItemStatusResponse {
  tableName: string | null
  slices: StationSlice[]
}
