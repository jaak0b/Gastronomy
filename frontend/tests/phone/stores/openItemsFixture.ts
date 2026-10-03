import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { stubLaptop, answer, inTurn, type LaptopReply, type StubbedLaptop } from '../../support/laptop'

export const OPEN_LIST = {
  tables: [
    {
      tableName: 'Tisch 12',
      openAmountCents: 700,
      items: [
        {
          orderItemId: 'item-1',
          orderId: 'order-1',
          globalOrderNumber: 137,
          itemName: 'Bratwurst',
          note: null,
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:00:00Z',
        },
        {
          orderItemId: 'item-2',
          orderId: 'order-1',
          globalOrderNumber: 137,
          itemName: 'Bratwurst',
          note: null,
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:00:00Z',
        },
      ],
    },
  ],
  itemsWithoutAnOrderCount: 0,
}

export const EMPTY_LIST = { tables: [], itemsWithoutAnOrderCount: 0 }

export const SETTLED = {
  settledOrderItemIds: ['item-1'],
  reappliedOrderItemIds: [],
  alreadySettledByOthersOrderItemIds: [],
}

export const TABLE_REPORT = {
  tableName: 'Tisch 12',
  openAmountCents: 400,
  orders: [
    {
      orderId: 'order-9',
      globalOrderNumber: 141,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Anna',
      items: [
        {
          orderItemId: 'lookup-open',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Bier',
          note: null,
          unitPriceCents: 400,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          fulfilledAtUtc: null,
          settledAtUtc: null,
        },
        {
          orderItemId: 'lookup-settled',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Wasser',
          note: null,
          unitPriceCents: 200,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          fulfilledAtUtc: '2026-09-05T18:25:00Z',
          settledAtUtc: '2026-09-05T18:40:00Z',
        },
      ],
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

export async function storeWithTheOpenList(replies: LaptopReply[]) {
  const laptop = laptopAnsweringInTurn([answer(OPEN_LIST), ...replies])
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.language = 'de'
  const openItems = useOpenItemsStore()
  await openItems.load()
  return { openItems, laptop }
}

export const REPORT_AFTER_THE_NEW_ORDER = {
  tableName: 'Tisch 12',
  openAmountCents: 1100,
  orders: [
    {
      orderId: 'order-8',
      globalOrderNumber: 140,
      createdAtUtc: '2026-09-05T18:10:00Z',
      staffMemberName: 'Anna',
      items: [
        {
          orderItemId: 'older-open',
          orderId: 'order-8',
          globalOrderNumber: 140,
          itemName: 'Bier',
          note: null,
          unitPriceCents: 400,
          orderedAtUtc: '2026-09-05T18:10:00Z',
          fulfilledAtUtc: null,
          settledAtUtc: null,
        },
      ],
    },
    {
      orderId: 'order-9',
      globalOrderNumber: 141,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Anna',
      items: [
        {
          orderItemId: 'new-open',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Bratwurst',
          note: null,
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          fulfilledAtUtc: null,
          settledAtUtc: null,
        },
        {
          orderItemId: 'new-settled',
          orderId: 'order-9',
          globalOrderNumber: 141,
          itemName: 'Wasser',
          note: null,
          unitPriceCents: 200,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          fulfilledAtUtc: null,
          settledAtUtc: '2026-09-05T18:21:00Z',
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
    fulfilledAtUtc: null,
    settledAtUtc,
  }
}

export const REPORT_WITH_TWO_ORDERS = {
  tableName: 'Tisch 12',
  openAmountCents: 1600,
  orders: [
    {
      orderId: 'order-a',
      globalOrderNumber: 150,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Anna',
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
      items: [positionOf('b-open', null)],
    },
  ],
}
