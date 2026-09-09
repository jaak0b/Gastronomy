import type { DeliveryMode, DraftLine, DraftOrder } from './apiTypes'
import type { SendFailureMessage } from './sendFailure'
import { noSendProgress, SEND_STATES, type SendProgress } from './sendProgress'

export const DRAFT_STORAGE_KEY = 'draftOrder'
export const SEND_PROGRESS_STORAGE_KEY = 'draftOrderSend'

const DELIVERY_MODES: DeliveryMode[] = ['together', 'asItComes']

export function emptyDraft(): DraftOrder {
  return { tableName: '', note: null, lines: [], clientOrderId: null, deliveryModes: {} }
}

function toDeliveryModes(value: unknown): Record<string, DeliveryMode> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return {}
  }
  const readable: Record<string, DeliveryMode> = {}
  for (const [stationId, mode] of Object.entries(value as Record<string, unknown>)) {
    const known = DELIVERY_MODES.find((candidate) => candidate === mode)
    if (known !== undefined) {
      readable[stationId] = known
    }
  }
  return readable
}

function toDraftLine(value: unknown): DraftLine | null {
  if (typeof value !== 'object' || value === null) {
    return null
  }
  const candidate = value as Record<string, unknown>
  if (typeof candidate.catalogItemId !== 'string') {
    return null
  }
  return {
    catalogItemId: candidate.catalogItemId,
    note: typeof candidate.note === 'string' ? candidate.note : null,
    stationId:
      typeof candidate.stationId === 'string' ? candidate.stationId : null,
    name: typeof candidate.name === 'string' ? candidate.name : '',
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
    deliveryModes: toDeliveryModes(candidate.deliveryModes),
  }
}

export type DraftRestoration =
  | { outcome: 'nothingStored'; draft: DraftOrder }
  | { outcome: 'restored'; draft: DraftOrder }
  | { outcome: 'unreadableDraftDiscarded'; draft: DraftOrder }

function parsedDraft(stored: string): DraftOrder | null {
  try {
    return toDraftOrder(JSON.parse(stored))
  } catch {
    return null
  }
}

export function restoreDraft(): DraftRestoration {
  const stored = localStorage.getItem(DRAFT_STORAGE_KEY)
  if (stored === null) {
    return { outcome: 'nothingStored', draft: emptyDraft() }
  }
  const restored = parsedDraft(stored)
  if (restored === null) {
    clearDraft()
    return { outcome: 'unreadableDraftDiscarded', draft: emptyDraft() }
  }
  return { outcome: 'restored', draft: restored }
}

export function saveDraft(draft: DraftOrder): void {
  localStorage.setItem(
    DRAFT_STORAGE_KEY,
    JSON.stringify({
      tableName: draft.tableName,
      note: draft.note,
      lines: draft.lines.map((line) => ({
        catalogItemId: line.catalogItemId,
        note: line.note,
        stationId: line.stationId,
        name: line.name,
      })),
      clientOrderId: draft.clientOrderId,
      deliveryModes: { ...draft.deliveryModes },
    }),
  )
}

export function clearDraft(): void {
  localStorage.removeItem(DRAFT_STORAGE_KEY)
  localStorage.removeItem(SEND_PROGRESS_STORAGE_KEY)
}

function toSendFailureMessage(value: unknown): SendFailureMessage | null {
  if (typeof value !== 'object' || value === null) {
    return null
  }
  const candidate = value as Record<string, unknown>
  if (typeof candidate.key !== 'string') {
    return null
  }
  return { key: candidate.key }
}

function toSendProgress(value: unknown): SendProgress | null {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return null
  }
  const candidate = value as Record<string, unknown>
  const state = SEND_STATES.find((known) => known === candidate.state)
  if (state === undefined || typeof candidate.attempts !== 'number') {
    return null
  }
  return {
    state,
    attempts: candidate.attempts,
    settleOnSend: candidate.settleOnSend === true,
    anAttemptWentUnanswered: candidate.anAttemptWentUnanswered === true,
    failure: toSendFailureMessage(candidate.failure),
  }
}

export function restoreSendProgress(): SendProgress {
  const stored = localStorage.getItem(SEND_PROGRESS_STORAGE_KEY)
  if (stored === null) {
    return noSendProgress()
  }
  try {
    return toSendProgress(JSON.parse(stored)) ?? noSendProgress()
  } catch {
    return noSendProgress()
  }
}

export function saveSendProgress(progress: SendProgress): void {
  localStorage.setItem(
    SEND_PROGRESS_STORAGE_KEY,
    JSON.stringify({
      state: progress.state,
      attempts: progress.attempts,
      settleOnSend: progress.settleOnSend,
      anAttemptWentUnanswered: progress.anAttemptWentUnanswered,
      failure: progress.failure === null ? null : { key: progress.failure.key },
    }),
  )
}

function persisted(draft: DraftOrder): DraftOrder {
  saveDraft(draft)
  return draft
}

function withLines(draft: DraftOrder, lines: DraftLine[]): DraftOrder {
  return persisted({ ...draft, lines })
}

export function addLine(draft: DraftOrder, line: DraftLine): DraftOrder {
  return withLines(draft, [...draft.lines, { ...line }])
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

export function setDeliveryMode(
  draft: DraftOrder,
  stationId: string,
  deliveryMode: DeliveryMode,
): DraftOrder {
  return persisted({
    ...draft,
    deliveryModes: { ...draft.deliveryModes, [stationId]: deliveryMode },
  })
}
