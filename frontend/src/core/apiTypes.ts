export interface CatalogItem {
  id: string
  name: string
  categoryName: string
  priceCents: number
  sortOrder: number
  isAvailable: boolean
  stationIds: string[]
}

export interface CatalogCategory {
  name: string
  sortOrder: number
}

export interface CatalogStation {
  id: string
  name: string
  sortOrder: number
}

export interface Catalog {
  version: string
  categories: CatalogCategory[]
  items: CatalogItem[]
  stations: CatalogStation[]
}

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
}

export type OrderStatus = 'Accepted' | 'Printing' | 'Printed' | 'NeedsAttention'

export type PrintJobStatus =
  | 'Queued'
  | 'Blocked'
  | 'Printing'
  | 'Unknown'
  | 'Failed'
  | 'Printed'
  | 'HandledOnPaper'

export interface OrderSubmitItem {
  catalogItemId: string
  unitPriceCents: number
  note: string | null
  stationId: string | null
}

export interface OrderSubmitRequest {
  clientOrderId: string
  tableName: string
  note: string | null
  items: OrderSubmitItem[]
}

export interface OrderSubmitResponse {
  orderId: string
  globalOrderNumber: number
  status: OrderStatus
  totalCents: number
  createdAtUtc: string
  stationOrders: {
    stationOrderId: string
    stationId: string
    stationName: string
    stationOrderNumber: number
    status: PrintJobStatus
    itemIds: string[]
  }[]
}

export interface StaffMember {
  id: string
  name: string
}

export type AppLanguage = 'de' | 'en'

export interface SessionInfo {
  deviceId: string
  staffMember: StaffMember
  language: AppLanguage
}

export interface RedeemResponse {
  deviceId: string
  deviceToken: string
  staffMember: StaffMember
  language: AppLanguage
}

export interface PrinterStatusRow {
  stationId: string
  name: string
  isOnline: boolean
  isPaperEnd: boolean
  isPaperNearEnd: boolean
  isCoverOpen: boolean
  isFaulty: boolean
  lastChangedAtUtc: string
}

export interface StationScreenOrderRow {
  stationOrderId: string
  orderId: string
  globalOrderNumber: number
  stationOrderNumber: number
  tableName: string
  orderCreatedAtUtc: string
  status: PrintJobStatus
  canHandleOnPaper: boolean
  canHandleOnPaperReasonKey: string | null
  copyNumber: number
  orderNote: string | null
  items: { quantity: number; itemName: string; itemNote: string | null }[]
}
