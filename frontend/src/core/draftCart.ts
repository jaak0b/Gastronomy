import { z } from 'zod'
import { deliveryModeSchema } from '../shared/api/apiSchemas'
import type {
  DeliveryMode,
  DraftLine,
  DraftOrder,
  OrderSettlementLine,
  OrderSubmitItem,
  OrderSubmitRequest,
  StationDeliveryMode,
} from '../shared/api/apiTypes'
import type { SendFailureMessage } from './sendFailure'
import { noSendProgress, SEND_STATES, type SendProgress } from './sendProgress'

export const DRAFT_STORAGE_KEY = 'draftOrder'
export const SEND_PROGRESS_STORAGE_KEY = 'draftOrderSend'

const draftLineSchema: z.ZodType<DraftLine> = z.strictObject({
  catalogItemId: z.string(),
  note: z.string().nullable(),
  stationId: z.string().nullable(),
  name: z.string(),
  stationName: z.string(),
})

const draftOrderSchema: z.ZodType<DraftOrder> = z.strictObject({
  festivalId: z.string().nullable(),
  tableName: z.string(),
  note: z.string().nullable(),
  lines: z.array(draftLineSchema),
  clientOrderId: z.string().nullable(),
  deliveryModes: z.record(z.string(), deliveryModeSchema),
})

const sendFailureSchema: z.ZodType<SendFailureMessage> = z.strictObject({
  key: z.string(),
  parameters: z.record(z.string(), z.union([z.string(), z.number()])).optional(),
})

const settlementLineSchema: z.ZodType<OrderSettlementLine> = z.strictObject({
  paidPriceCents: z.number().int().nonnegative(),
  paymentNotice: z.string().nullable(),
})

const submitItemSchema: z.ZodType<OrderSubmitItem> = z.strictObject({
  catalogItemId: z.string(),
  unitPriceCents: z.number().int().nonnegative(),
  note: z.string().nullable(),
  stationId: z.string().nullable(),
  settlement: settlementLineSchema.nullable(),
})

const stationDeliveryModeSchema: z.ZodType<StationDeliveryMode> = z.strictObject({
  stationId: z.string(),
  deliveryMode: deliveryModeSchema,
})

const submitRequestSchema: z.ZodType<OrderSubmitRequest> = z.strictObject({
  clientOrderId: z.string(),
  tableName: z.string(),
  note: z.string().nullable(),
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
    tableName: '',
    note: null,
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
      tableName: draft.tableName,
      note: draft.note,
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
