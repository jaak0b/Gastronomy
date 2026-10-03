import { flushPromises, mount } from '@vue/test-utils'
import OpenItems from '../../../src/phone/views/OpenItems.vue'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'
import { answer, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../support/laptop'

export const OPEN_LIST = {
  tables: [
    {
      tableName: '12',
      openAmountCents: 700,
      items: [
        {
          orderItemId: 'item-1',
          orderId: 'order-1',
          globalOrderNumber: 137,
          itemName: 'Bratwurst',
          note: 'ohne Zwiebeln',
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:00:00Z',
        },
        {
          orderItemId: 'item-2',
          orderId: 'order-1',
          globalOrderNumber: 137,
          itemName: 'Bier',
          note: null,
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:00:00Z',
        },
      ],
    },
  ],
  itemsWithoutAnOrderCount: 0,
}

export const SETTLED = {
  settledOrderItemIds: ['item-1'],
  reappliedOrderItemIds: [],
  alreadySettledByOthersOrderItemIds: [],
}

export const TWO_TABLES = {
  ...OPEN_LIST,
  tables: [
    ...OPEN_LIST.tables,
    {
      tableName: '123',
      openAmountCents: 500,
      items: [
        {
          orderItemId: 'item-7',
          orderId: 'order-7',
          globalOrderNumber: 141,
          itemName: 'Bier',
          note: null,
          unitPriceCents: 500,
          orderedAtUtc: '2026-09-05T18:20:00Z',
        },
      ],
    },
  ],
}

export const TABLE_REPORT = {
  tableName: 'Tisch 12',
  openAmountCents: 1200,
  orders: [
    {
      orderId: 'order-137',
      globalOrderNumber: 137,
      createdAtUtc: '2026-09-05T18:00:00Z',
      staffMemberName: 'Anna',
      items: [
        {
          orderItemId: 'item-plain',
          orderId: 'order-137',
          globalOrderNumber: 137,
          itemName: 'Bratwurst',
          note: 'ohne Zwiebeln',
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:00:00Z',
          fulfilledAtUtc: null,
          settledAtUtc: null,
        },
      ],
    },
    {
      orderId: 'order-138',
      globalOrderNumber: 138,
      createdAtUtc: '2026-09-05T18:10:00Z',
      staffMemberName: 'Bernd',
      items: [
        {
          orderItemId: 'item-half',
          orderId: 'order-138',
          globalOrderNumber: 138,
          itemName: 'Bier',
          note: null,
          unitPriceCents: 400,
          orderedAtUtc: '2026-09-05T18:10:00Z',
          fulfilledAtUtc: '2026-09-05T18:15:00Z',
          settledAtUtc: null,
        },
        {
          orderItemId: 'item-waiting',
          orderId: 'order-138',
          globalOrderNumber: 138,
          itemName: 'Cola',
          note: null,
          unitPriceCents: 150,
          orderedAtUtc: '2026-09-05T18:10:00Z',
          fulfilledAtUtc: null,
          settledAtUtc: null,
        },
        {
          orderItemId: 'item-settled',
          orderId: 'order-138',
          globalOrderNumber: 138,
          itemName: 'Wasser',
          note: null,
          unitPriceCents: 200,
          orderedAtUtc: '2026-09-05T18:10:00Z',
          fulfilledAtUtc: '2026-09-05T18:15:00Z',
          settledAtUtc: '2026-09-05T18:40:00Z',
        },
      ],
    },
    {
      orderId: 'order-139',
      globalOrderNumber: 139,
      createdAtUtc: '2026-09-05T18:20:00Z',
      staffMemberName: 'Clara',
      items: [
        {
          orderItemId: 'item-done',
          orderId: 'order-139',
          globalOrderNumber: 139,
          itemName: 'Kuchen',
          note: null,
          unitPriceCents: 300,
          orderedAtUtc: '2026-09-05T18:20:00Z',
          fulfilledAtUtc: '2026-09-05T18:30:00Z',
          settledAtUtc: null,
        },
      ],
    },
  ],
}

export function laptopWithALookup(report: unknown, list: unknown = OPEN_LIST): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer(list))
    .answers('GET', '/api/open-items/table-names', answer({ tableNames: ['Tisch 12'] }))
    .answers('GET', '/api/open-items/table', answer(report))
    .answers('ANY', (call) => call.method !== 'GET', answer(SETTLED))
}

export function openItemsLaptop(
  list: unknown = OPEN_LIST,
  settlement: LaptopReply = answer(SETTLED),
): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer(list))
    .answers('ANY', (call) => call.method !== 'GET', settlement)
}

export async function openEveryTable(screen: Awaited<ReturnType<typeof mountScreen>>): Promise<void> {
  for (const title of screen.findAll('[data-test="open-table-title"]')) {
    await title.trigger('click')
  }
  await flushPromises()
}

type MountedScreen = Awaited<ReturnType<typeof mountScreen>>

type ScreenPart = ReturnType<MountedScreen['get']>

export function lineNamed(container: MountedScreen | ScreenPart, itemName: string): ScreenPart {
  const line = container
    .findAll('[data-test="open-line"]')
    .find((candidate) => candidate.get('[data-test="line-name"]').text() === itemName)
  if (line === undefined) {
    throw new Error(`No open line names ${itemName}.`)
  }
  return line
}

export function lineOf(screen: MountedScreen, tableName: string, itemName: string): ScreenPart {
  return lineNamed(screen.get(`[data-test="open-table"][data-test-id="${tableName}"]`), itemName)
}

export function lookupCard(screen: MountedScreen, orderNumber: number): ScreenPart {
  return screen.get(`[data-test="lookup-card"][data-test-id="${orderNumber}"]`)
}

export function isHalfTaken(part: MountedScreen | ScreenPart, selector: string): boolean {
  return part.findComponent(selector).props('indeterminate') === true
}

export async function mountScreen() {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.language = 'de'
  const screen = mount(OpenItems, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
  await flushPromises()
  return screen
}
