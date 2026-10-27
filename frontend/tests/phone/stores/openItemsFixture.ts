import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { stubLaptop, answer, inTurn, type LaptopReply, type StubbedLaptop } from '../../support/laptop'

export const SETTLED = {
  settledOrderItemIds: ['item-1'],
  reappliedOrderItemIds: [],
  alreadySettledByOthersOrderItemIds: [],
}

export const TABLE_PATH = '/api/open-items/tables/table-12/search'

export const TABLE_REPORT = {
  tableId: 'table-12',
  tableName: 'Tisch 12',
  orders: [
    {
      orderId: 'order-9',
      globalOrderNumber: 141,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Anna',
      notCancelledItemCount: 2,
      handedOutItemCount: 1,
      items: [
        {
          orderItemId: 'bier-open',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Bier',
          note: null,
          unitPriceCents: 400,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          state: 'open' as const,
          stateSetAtUtc: '2026-09-05T18:20:00Z',
          wasHandedOutWhenCancelled: false,
          settledAtUtc: null,
          chargedPriceCents: null,
        },
        {
          orderItemId: 'wasser-settled',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Wasser',
          note: null,
          unitPriceCents: 200,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          state: 'handedOut' as const,
          stateSetAtUtc: '2026-09-05T18:25:00Z',
          wasHandedOutWhenCancelled: false,
          settledAtUtc: '2026-09-05T18:40:00Z',
          chargedPriceCents: 200,
        },
      ],
    },
  ],
}

function bratwurst(orderItemId: string) {
  return {
    orderItemId,
    orderId: 'order-1',
    globalOrderNumber: 137,
    itemName: 'Bratwurst',
    note: null,
    unitPriceCents: 350,
    orderedAtUtc: '2026-09-05T18:00:00Z',
    state: 'open' as const,
    stateSetAtUtc: '2026-09-05T18:00:00Z',
    wasHandedOutWhenCancelled: false,
    settledAtUtc: null,
    chargedPriceCents: null,
  }
}

export const TWO_BRATWURST = {
  tableId: 'table-12',
  tableName: 'Tisch 12',
  orders: [
    {
      orderId: 'order-1',
      globalOrderNumber: 137,
      createdAtUtc: '2026-09-05T18:00:00Z',
      staffMemberName: 'Anna',
      notCancelledItemCount: 2,
      handedOutItemCount: 0,
      items: [bratwurst('item-1'), bratwurst('item-2')],
    },
  ],
}

export interface DeferredResponse {
  promise: Promise<Response>
  answerWith: (payload: unknown) => void
}

export function deferredResponse(): DeferredResponse {
  let answer: (response: Response) => void = () => undefined
  const promise = new Promise<Response>((resolve) => {
    answer = resolve
  })
  return {
    promise,
    answerWith: (payload) => answer(new Response(JSON.stringify(payload), { status: 200 })),
  }
}

export function laptopAnsweringInTurn(replies: LaptopReply[]): StubbedLaptop {
  return stubLaptop().answersEverythingElse(inTurn(...replies))
}

export function aWaiterPhone(): void {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.language = 'de'
}

export async function storeShowingTisch12(replies: LaptopReply[], report: unknown = TABLE_REPORT) {
  const laptop = laptopAnsweringInTurn([answer(report), ...replies])
  aWaiterPhone()
  const openItems = useOpenItemsStore()
  openItems.openTable('table-12')
  await openItems.load()
  return { openItems, laptop }
}

export const REPORT_AFTER_THE_NEW_ORDER = {
  tableId: 'table-12',
  tableName: 'Tisch 12',
  orders: [
    {
      orderId: 'order-8',
      globalOrderNumber: 140,
      createdAtUtc: '2026-09-05T18:10:00Z',
      staffMemberName: 'Anna',
      notCancelledItemCount: 1,
      handedOutItemCount: 0,
      items: [
        {
          orderItemId: 'older-open',
          orderId: 'order-8',
          globalOrderNumber: 140,
          itemName: 'Bier',
          note: null,
          unitPriceCents: 400,
          orderedAtUtc: '2026-09-05T18:10:00Z',
          state: 'open' as const,
          stateSetAtUtc: '2026-09-05T18:10:00Z',
          wasHandedOutWhenCancelled: false,
          settledAtUtc: null,
          chargedPriceCents: null,
        },
      ],
    },
    {
      orderId: 'order-9',
      globalOrderNumber: 141,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Anna',
      notCancelledItemCount: 2,
      handedOutItemCount: 0,
      items: [
        {
          orderItemId: 'new-open',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Bratwurst',
          note: null,
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          state: 'open' as const,
          stateSetAtUtc: '2026-09-05T18:20:00Z',
          wasHandedOutWhenCancelled: false,
          settledAtUtc: null,
          chargedPriceCents: null,
        },
        {
          orderItemId: 'new-settled',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Wasser',
          note: null,
          unitPriceCents: 200,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          state: 'open' as const,
          stateSetAtUtc: '2026-09-05T18:20:00Z',
          wasHandedOutWhenCancelled: false,
          settledAtUtc: '2026-09-05T18:21:00Z',
          chargedPriceCents: 200,
        },
      ],
    },
  ],
}

export const REPORT_WITH_THE_NEW_ITEM_SETTLED = {
  ...REPORT_AFTER_THE_NEW_ORDER,
  orders: [
    REPORT_AFTER_THE_NEW_ORDER.orders[0],
    {
      ...REPORT_AFTER_THE_NEW_ORDER.orders[1],
      items: REPORT_AFTER_THE_NEW_ORDER.orders[1].items.map((item) => ({
        ...item,
        settledAtUtc: '2026-09-05T18:21:00Z',
        chargedPriceCents: item.unitPriceCents,
      })),
    },
  ],
}

export function positionOf(orderItemId: string, settledAtUtc: string | null) {
  return {
    orderItemId,
    orderId: 'order-x',
    globalOrderNumber: 150,
    itemName: 'Bier',
    note: null,
    unitPriceCents: 400,
    orderedAtUtc: '2026-09-05T18:20:00Z',
    state: 'open' as const,
    stateSetAtUtc: '2026-09-05T18:20:00Z',
    wasHandedOutWhenCancelled: false,
    settledAtUtc,
    chargedPriceCents: null,
  }
}

export const REPORT_WITH_TWO_ORDERS = {
  tableId: 'table-12',
  tableName: 'Tisch 12',
  orders: [
    {
      orderId: 'order-a',
      globalOrderNumber: 150,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Anna',
      notCancelledItemCount: 3,
      handedOutItemCount: 0,
      items: [
        positionOf('a-open-1', null),
        positionOf('a-open-2', null),
        positionOf('a-settled', '2026-09-05T18:40:00Z'),
      ],
    },
    {
      orderId: 'order-b',
      globalOrderNumber: 151,
      createdAtUtc: '2026-09-05T18:30:00Z',
      staffMemberName: 'Anna',
      notCancelledItemCount: 1,
      handedOutItemCount: 0,
      items: [positionOf('b-open', null)],
    },
  ],
}
