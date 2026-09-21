import type { DraftLine, DraftOrder } from './draftCart'
import { CatalogView, OrderDeliveryModeRequest, PlaceOrderRequest } from '../../shared/api/generatedSchemas'
import { findCatalogItem } from './basket'
import { saveDraft } from './draftCart'
import { splitSettlement } from './settlementSplit'

export interface ConfirmedSettlement {
  amountPaidCents: number
  paymentNotice: string | null
}

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
    hex.substring(0, 8),
    hex.substring(8, 12),
    hex.substring(12, 16),
    hex.substring(16, 20),
    hex.substring(20),
  ].join('-')
}

export function withClientOrderIdIfMissing(draft: DraftOrder): DraftOrder {
  if (draft.clientOrderId !== null) {
    return draft
  }
  const identified: DraftOrder = { ...draft, clientOrderId: newSubmissionId() }
  saveDraft(identified)
  return identified
}

function priceOnTheMenu(catalog: CatalogView, line: DraftLine): number {
  return findCatalogItem(catalog, line.catalogItemId)?.priceCents ?? 0
}

export function buildSubmitRequest(
  draft: DraftOrder,
  catalog: CatalogView,
  settlement: ConfirmedSettlement | null,
  deliveryModes: readonly OrderDeliveryModeRequest[],
): PlaceOrderRequest {
  const clientOrderId = draft.clientOrderId
  if (clientOrderId === null) {
    throw new Error('A draft without a clientOrderId must not be submitted')
  }
  const pricedLines = draft.lines.map((line) => ({
    line,
    unitPriceCents: priceOnTheMenu(catalog, line),
  }))
  const settlementLines =
    settlement === null
      ? []
      : splitSettlement(settlement.amountPaidCents, pricedLines, settlement.paymentNotice ?? '')
  return {
    clientOrderId,
    tableName: draft.tableName.trim(),
    items: pricedLines.map(({ line, unitPriceCents }, index) => ({
      catalogItemId: line.catalogItemId,
      unitPriceCents,
      note: line.note,
      stationId: line.stationId,
      settlement: settlementLines[index] ?? null,
    })),
    deliveryModes: [...deliveryModes],
  }
}
