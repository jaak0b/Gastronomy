import { z } from 'zod'
import { DeliveryMode, OrderDeliveryModeRequest, OrderItemRequest, PlaceOrderRequest } from '../../shared/api/generatedSchemas'
import type { TablePlanAreaView } from '../../shared/api/generatedSchemas'
import type { SendFailureMessage } from './sendFailure'
import { noSendProgress, SEND_STATES, type SendProgress } from './sendProgress'

export interface DraftLine {
  catalogItemId: string
  note: string | null
  stationId: string | null
  name: string
  stationName: string
}

export interface DraftTable {
  tableId: string
  name: string
}

export interface DraftOrder {
  festivalId: string | null
  table: DraftTable | null
  lines: DraftLine[]
  clientOrderId: string | null
  deliveryModes: Record<string, DeliveryMode>
}

export const DRAFT_STORAGE_KEY = 'draftOrder'
export const SEND_PROGRESS_STORAGE_KEY = 'draftOrderSend'

const draftLineSchema: z.ZodType<DraftLine> = z.strictObject({
  catalogItemId: z.string(),
  note: z.string().nullable(),
  stationId: z.string().nullable(),
  name: z.string(),
  stationName: z.string(),
})

const draftTableSchema: z.ZodType<DraftTable> = z.strictObject({
  tableId: z.string(),
  name: z.string(),
})

const draftOrderSchema: z.ZodType<DraftOrder> = z.strictObject({
  festivalId: z.string().nullable(),
  table: draftTableSchema.nullable(),
  lines: z.array(draftLineSchema),
  clientOrderId: z.string().nullable(),
  deliveryModes: z.record(z.string(), DeliveryMode),
})

const sendFailureSchema: z.ZodType<SendFailureMessage> = z.strictObject({
  key: z.string(),
  parameters: z.record(z.string(), z.union([z.string(), z.number()])).optional(),
})

const submitItemSchema: z.ZodType<OrderItemRequest> = z.strictObject({
  catalogItemId: z.string(),
  unitPriceCents: z.number().int().nonnegative(),
  note: z.string().nullable(),
  stationId: z.string().nullable(),
})

const stationDeliveryModeSchema: z.ZodType<OrderDeliveryModeRequest> = z.strictObject({
  stationId: z.string(),
  deliveryMode: DeliveryMode,
})

const submitRequestSchema: z.ZodType<PlaceOrderRequest> = z.strictObject({
  clientOrderId: z.string(),
  tableId: z.string(),
  items: z.array(submitItemSchema),
  deliveryModes: z.array(stationDeliveryModeSchema),
})

const sendProgressSchema: z.ZodType<SendProgress> = z
  .strictObject({
    state: z.enum(SEND_STATES),
    attempts: z.number().int().nonnegative(),
    failure: sendFailureSchema.nullable(),
    unresolvedAttempt: submitRequestSchema.nullable(),
  })
  .refine(
    (progress) =>
      progress.unresolvedAttempt !== null ||
      (progress.state !== 'sending' && progress.state !== 'failed'),
  )

export function emptyDraft(): DraftOrder {
  return {
    festivalId: null,
    table: null,
    lines: [],
    clientOrderId: null,
    deliveryModes: {},
  }
}

export type DraftRestoration =
  | { outcome: 'nothingStored'; draft: DraftOrder }
  | { outcome: 'restored'; draft: DraftOrder }
  | { outcome: 'unreadableDraftDiscarded'; draft: DraftOrder }

function parsedDraft(stored: string): DraftOrder | null {
  try {
    const parsed = draftOrderSchema.safeParse(JSON.parse(stored))
    return parsed.success ? parsed.data : null
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
      festivalId: draft.festivalId,
      table: draft.table === null ? null : { tableId: draft.table.tableId, name: draft.table.name },
      lines: draft.lines.map((line) => ({
        catalogItemId: line.catalogItemId,
        note: line.note,
        stationId: line.stationId,
        name: line.name,
        stationName: line.stationName,
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

export function restoreSendProgress(): SendProgress {
  const stored = localStorage.getItem(SEND_PROGRESS_STORAGE_KEY)
  if (stored === null) {
    return noSendProgress()
  }
  try {
    const parsed = sendProgressSchema.safeParse(JSON.parse(stored))
    return parsed.success ? parsed.data : noSendProgress()
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
      failure:
        progress.failure === null
          ? null
          : progress.failure.parameters === undefined
            ? { key: progress.failure.key }
            : { key: progress.failure.key, parameters: progress.failure.parameters },
      unresolvedAttempt: progress.unresolvedAttempt,
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
  stationName: string,
): DraftOrder {
  return withLines(
    draft,
    draft.lines.map((line, position) =>
      position === index ? { ...line, stationId, stationName } : line,
    ),
  )
}

export function draftIsForAnotherFestival(
  draft: DraftOrder,
  runningFestivalId: string | null,
): boolean {
  return draft.festivalId !== runningFestivalId
}

export function stampFestival(draft: DraftOrder, festivalId: string): DraftOrder {
  return persisted({ ...draft, festivalId })
}

export function setTable(draft: DraftOrder, table: DraftTable): DraftOrder {
  return persisted({ ...draft, table: { ...table } })
}

export function shownNameOf(table: DraftTable, areas: readonly TablePlanAreaView[]): string {
  return (
    areas.flatMap((area) => area.tables).find((planTable) => planTable.tableId === table.tableId)?.name ??
    table.name
  )
}

export type OrderOpening = 'continueTheDraft' | 'takeTheTable' | 'askBeforeDiscardingTheDraft'

export function openingTheOrderAt(draft: DraftOrder, tableId: string): OrderOpening {
  if (draft.table === null || draft.lines.length === 0) {
    return 'takeTheTable'
  }
  return draft.table.tableId === tableId ? 'continueTheDraft' : 'askBeforeDiscardingTheDraft'
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
