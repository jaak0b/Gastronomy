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

export interface TableSuggestion {
  label: string
  sortOrder: number
}

export interface Catalog {
  version: string
  categories: CatalogCategory[]
  items: CatalogItem[]
  stations: CatalogStation[]
  tableSuggestions: TableSuggestion[]
}

export interface DraftLine {
  catalogItemId: string
  quantity: number
  note: string | null
  stationId: string | null
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
  stationId: string | null
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
  stationId: string
  stationName: string
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
    stationId: string
    stationName: string
    sequenceNumber: number
    status: TicketStatus
    lineIds: string[]
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

export interface StationTicketRow {
  ticketId: string
  orderId: string
  globalOrderNumber: number
  sequenceNumber: number
  tableLabel: string
  orderCreatedAtUtc: string
  status: TicketStatus
  canAcknowledge: boolean
  canAcknowledgeReasonKey: string | null
  reprintCount: number
  orderNote: string | null
  lines: { quantity: number; itemName: string; lineNote: string | null }[]
}
