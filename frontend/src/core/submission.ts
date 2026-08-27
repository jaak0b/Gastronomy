import type { DraftOrder, OrderSubmitRequest } from './apiTypes'
import { saveDraft } from './draftCart'

export function ensureClientOrderId(draft: DraftOrder): DraftOrder {
  if (draft.clientOrderId !== null) {
    return draft
  }
  const identified: DraftOrder = { ...draft, clientOrderId: crypto.randomUUID() }
  saveDraft(identified)
  return identified
}

export function buildSubmitRequest(
  draft: DraftOrder,
  expectedTotalCents: number,
): OrderSubmitRequest {
  const clientOrderId = draft.clientOrderId
  if (clientOrderId === null) {
    throw new Error('A draft without a clientOrderId must not be submitted')
  }
  return {
    clientOrderId,
    tableLabel: draft.tableLabel,
    note: draft.note,
    expectedTotalCents,
    lines: draft.lines.map((line) => ({
      catalogItemId: line.catalogItemId,
      quantity: line.quantity,
      note: line.note,
      stationId: line.stationId,
    })),
  }
}
