import type { DraftLine, DraftOrder } from './apiTypes'

export const DRAFT_STORAGE_KEY = 'draftOrder'

export function emptyDraft(): DraftOrder {
  return { tableName: '', note: null, lines: [], clientOrderId: null }
}

function toDraftLine(value: unknown): DraftLine | null {
  if (typeof value !== 'object' || value === null) {
    return null
  }
  const candidate = value as Record<string, unknown>
  if (typeof candidate.catalogItemId !== 'string' || typeof candidate.quantity !== 'number') {
    return null
  }
  return {
    catalogItemId: candidate.catalogItemId,
    quantity: candidate.quantity,
    note: typeof candidate.note === 'string' ? candidate.note : null,
    stationId:
      typeof candidate.stationId === 'string' ? candidate.stationId : null,
    name: typeof candidate.name === 'string' ? candidate.name : '',
    unitPriceCents: typeof candidate.unitPriceCents === 'number' ? candidate.unitPriceCents : 0,
  }
}

function toDraftOrder(value: unknown): DraftOrder | null {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return null
  }
  const candidate = value as Record<string, unknown>
  if (typeof candidate.tableName !== 'string' || !Array.isArray(candidate.lines)) {
    return null
  }
  const lines: DraftLine[] = []
  for (const entry of candidate.lines) {
    const line = toDraftLine(entry)
    if (line === null) {
      return null
    }
    lines.push(line)
  }
  return {
    tableName: candidate.tableName,
    note: typeof candidate.note === 'string' ? candidate.note : null,
    lines,
    clientOrderId: typeof candidate.clientOrderId === 'string' ? candidate.clientOrderId : null,
  }
}

export function loadDraft(): DraftOrder {
  const stored = localStorage.getItem(DRAFT_STORAGE_KEY)
  if (stored === null) {
    return emptyDraft()
  }
  try {
    const restored = toDraftOrder(JSON.parse(stored))
    return restored === null ? emptyDraft() : restored
  } catch {
    return emptyDraft()
  }
}

export function saveDraft(draft: DraftOrder): void {
  localStorage.setItem(
    DRAFT_STORAGE_KEY,
    JSON.stringify({
      tableName: draft.tableName,
      note: draft.note,
      lines: draft.lines.map((line) => ({
        catalogItemId: line.catalogItemId,
        quantity: line.quantity,
        note: line.note,
        stationId: line.stationId,
        name: line.name,
        unitPriceCents: line.unitPriceCents,
      })),
      clientOrderId: draft.clientOrderId,
    }),
  )
}

export function clearDraft(): void {
  localStorage.removeItem(DRAFT_STORAGE_KEY)
}

function persisted(draft: DraftOrder): DraftOrder {
  saveDraft(draft)
  return draft
}

function withLines(draft: DraftOrder, lines: DraftLine[]): DraftOrder {
  return persisted({ ...draft, lines })
}

function joinsWith(existing: DraftLine, added: DraftLine): boolean {
  return (
    existing.catalogItemId === added.catalogItemId &&
    existing.stationId === added.stationId &&
    existing.note === null &&
    added.note === null
  )
}

export function addLine(draft: DraftOrder, line: DraftLine): DraftOrder {
  const position = draft.lines.findIndex((existing) => joinsWith(existing, line))
  if (position === -1) {
    return withLines(draft, [...draft.lines, { ...line }])
  }

  return withLines(
    draft,
    draft.lines.map((existing, index) =>
      index === position
        ? { ...existing, quantity: existing.quantity + line.quantity }
        : existing,
    ),
  )
}

export function setLineQuantity(draft: DraftOrder, index: number, quantity: number): DraftOrder {
  if (quantity <= 0) {
    return removeLine(draft, index)
  }
  return withLines(
    draft,
    draft.lines.map((line, position) => (position === index ? { ...line, quantity } : line)),
  )
}

export function removeLine(draft: DraftOrder, index: number): DraftOrder {
  return withLines(
    draft,
    draft.lines.filter((_, position) => position !== index),
  )
}

export function setLineNote(draft: DraftOrder, index: number, note: string | null): DraftOrder {
  return withLines(
    draft,
    draft.lines.map((line, position) => (position === index ? { ...line, note } : line)),
  )
}

export function setLineStation(
  draft: DraftOrder,
  index: number,
  stationId: string | null,
): DraftOrder {
  return withLines(
    draft,
    draft.lines.map((line, position) =>
      position === index ? { ...line, stationId: stationId } : line,
    ),
  )
}

export function setTableName(draft: DraftOrder, tableName: string): DraftOrder {
  return persisted({ ...draft, tableName })
}

export function setOrderNote(draft: DraftOrder, note: string | null): DraftOrder {
  return persisted({ ...draft, note })
}
