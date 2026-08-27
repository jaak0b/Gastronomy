export interface CatalogItem {
  id: string
  name: string
  categoryName: string
  priceCents: number
  sortOrder: number
  isAvailable: boolean
  locationIds: string[]
}

export interface CatalogCategory {
  name: string
  sortOrder: number
}

export interface CatalogLocation {
  id: string
  name: string
  sortOrder: number
}

export interface TableSuggestion {
  label: string
  sortOrder: number
}

export interface Catalog {
  version: string
  categories: CatalogCategory[]
  items: CatalogItem[]
  locations: CatalogLocation[]
  tableSuggestions: TableSuggestion[]
}

export interface DraftLine {
  catalogItemId: string
  quantity: number
  note: string | null
  productionLocationId: string | null
  name: string
  unitPriceCents: number
}

export interface DraftOrder {
  tableLabel: string
  note: string | null
  lines: DraftLine[]
  clientOrderId: string | null
}

export type OrderStatus = 'Accepted' | 'Printing' | 'Printed' | 'NeedsAttention'

export type TicketStatus =
  | 'Queued'
  | 'Blocked'
  | 'Printing'
  | 'Unknown'
  | 'Failed'
  | 'Printed'
  | 'PrintedOnTestPrinter'
  | 'HandledOnPaper'

export type PrintFailureReason =
  | 'PaperEnd'
  | 'CoverOpen'
  | 'Unreachable'
  | 'Timeout'
  | 'SocketDropped'
  | 'PrinterError'
  | 'StationDisabled'
  | 'StationFaulty'
  | 'TicketResolvedByHuman'

export interface OrderSubmitLine {
  catalogItemId: string
  quantity: number
  note: string | null
  productionLocationId: string | null
}

export interface OrderSubmitRequest {
  clientOrderId: string
  tableLabel: string
  note: string | null
  expectedTotalCents: number
  lines: OrderSubmitLine[]
}

export interface TicketSummary {
  ticketId: string
  locationId: string
  locationName: string
  sequenceNumber: number
  status: TicketStatus
  failureReason: PrintFailureReason | null
  printerHasPaper: boolean | null
}

export interface OrderSummary {
  orderId: string
  globalOrderNumber: number
  tableLabel: string
  totalCents: number
  status: OrderStatus
  createdAtUtc: string
  tickets: TicketSummary[]
}

export interface OrderSubmitResponse {
  orderId: string
  globalOrderNumber: number
  status: OrderStatus
  totalCents: number
  expectedTotalCents: number
  createdAtUtc: string
  tickets: {
    ticketId: string
    locationId: string
    locationName: string
    sequenceNumber: number
    status: TicketStatus
    lineIds: string[]
  }[]
}

export interface ServerPerson {
  id: string
  name: string
}

export type AppLanguage = 'de' | 'en'

export interface SessionInfo {
  deviceId: string
  serverPerson: ServerPerson
  language: AppLanguage
}

export interface RedeemResponse {
  deviceId: string
  deviceToken: string
  serverPerson: ServerPerson
  language: AppLanguage
}

export interface PrinterStatusRow {
  locationId: string
  name: string
  isOnline: boolean
  isPaperEnd: boolean
  isPaperNearEnd: boolean
  isCoverOpen: boolean
  isFaulty: boolean
  lastChangedAtUtc: string
}

export interface StationTicketRow {
  ticketId: string
  orderId: string
  globalOrderNumber: number
  sequenceNumber: number
  tableLabel: string
  createdAtUtc: string
  status: TicketStatus
  canAcknowledge: boolean
  refusalReasonKey: string | null
  reprintCount: number
  orderNote: string | null
  lines: { quantity: number; itemName: string; note: string | null }[]
}
