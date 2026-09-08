import type {
  Catalog,
  DraftLine,
  DraftOrder,
  OrderSubmitRequest,
  StationDeliveryMode,
} from './apiTypes'
import { findCatalogItem } from './basket'
import { saveDraft } from './draftCart'

const VERSION_FOUR_MASK = 0x0f
const VERSION_FOUR_BITS = 0x40
const VARIANT_MASK = 0x3f
const VARIANT_BITS = 0x80

function newSubmissionId(): string {
  const bytes = new Uint8Array(16)
  crypto.getRandomValues(bytes)
  bytes[6] = (bytes[6] & VERSION_FOUR_MASK) | VERSION_FOUR_BITS
  bytes[8] = (bytes[8] & VARIANT_MASK) | VARIANT_BITS
  const hex = [...bytes].map((byte) => byte.toString(16).padStart(2, '0')).join('')
  return [
    hex.slice(0, 8),
    hex.slice(8, 12),
    hex.slice(12, 16),
    hex.slice(16, 20),
    hex.slice(20),
  ].join('-')
}

export function ensureClientOrderId(draft: DraftOrder): DraftOrder {
  if (draft.clientOrderId !== null) {
    return draft
  }
  const identified: DraftOrder = { ...draft, clientOrderId: newSubmissionId() }
  saveDraft(identified)
  return identified
}

function priceOnTheMenu(catalog: Catalog, line: DraftLine): number {
  return findCatalogItem(catalog, line.catalogItemId)?.priceCents ?? 0
}

export function buildSubmitRequest(
  draft: DraftOrder,
  catalog: Catalog,
  settleOnSend: boolean,
  deliveryModes: readonly StationDeliveryMode[],
): OrderSubmitRequest {
  const clientOrderId = draft.clientOrderId
  if (clientOrderId === null) {
    throw new Error('A draft without a clientOrderId must not be submitted')
  }
  return {
    clientOrderId,
    tableName: draft.tableName,
    note: draft.note,
    settleOnSend,
    items: draft.lines.map((line) => ({
      catalogItemId: line.catalogItemId,
      unitPriceCents: priceOnTheMenu(catalog, line),
      note: line.note,
      stationId: line.stationId,
    })),
    deliveryModes: [...deliveryModes],
  }
}
