import { flushPromises, mount } from '@vue/test-utils'
import type { VCheckboxBtn } from 'vuetify/components'
import OpenItems from '../../../src/phone/views/OpenItems.vue'
import type { AppLanguage } from '../../../src/shared/core/deviceLanguage'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'
import { answer, stubLaptop, type LaptopReply, type StubbedLaptop } from '../../support/laptop'

function itemOfOrder137(orderItemId: string, itemName: string, note: string | null, unitPriceCents: number) {
  return {
    orderItemId,
    orderId: 'order-1',
    globalOrderNumber: 137,
    itemName,
    note,
    unitPriceCents,
    orderedAtUtc: '2026-09-05T18:00:00Z',
    state: 'open',
    stateSetAtUtc: '2026-09-05T18:00:00Z',
    settledAtUtc: null,
    chargedPriceCents: null,
  }
}

export const TISCH_12 = {
  tableId: 'table-12',
  tableName: 'Tisch 12',
  openAmountCents: 700,
  orders: [
    {
      orderId: 'order-1',
      globalOrderNumber: 137,
      createdAtUtc: '2026-09-05T18:00:00Z',
      staffMemberName: 'Anna',
      items: [
        itemOfOrder137('item-1', 'Bratwurst', 'ohne Zwiebeln', 350),
        itemOfOrder137('item-2', 'Bier', null, 350),
      ],
    },
  ],
}

export const ONE_FREE_ITEM = {
  ...TISCH_12,
  openAmountCents: 0,
  orders: [{ ...TISCH_12.orders[0], items: [itemOfOrder137('item-water', 'Leitungswasser', null, 0)] }],
}

export const SETTLED = {
  settledOrderItemIds: ['item-1'],
  reappliedOrderItemIds: [],
  alreadySettledByOthersOrderItemIds: [],
}

export const TABLE_REPORT = {
  tableId: 'table-12',
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
          state: 'open',
          stateSetAtUtc: '2026-09-05T18:00:00Z',
          settledAtUtc: null,
          chargedPriceCents: null,
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
          state: 'handedOut',
          stateSetAtUtc: '2026-09-05T18:15:00Z',
          settledAtUtc: null,
          chargedPriceCents: null,
        },
        {
          orderItemId: 'item-waiting',
          orderId: 'order-138',
          globalOrderNumber: 138,
          itemName: 'Cola',
          note: null,
          unitPriceCents: 150,
          orderedAtUtc: '2026-09-05T18:10:00Z',
          state: 'open',
          stateSetAtUtc: '2026-09-05T18:10:00Z',
          settledAtUtc: null,
          chargedPriceCents: null,
        },
        {
          orderItemId: 'item-settled',
          orderId: 'order-138',
          globalOrderNumber: 138,
          itemName: 'Wasser',
          note: null,
          unitPriceCents: 200,
          orderedAtUtc: '2026-09-05T18:10:00Z',
          state: 'handedOut',
          stateSetAtUtc: '2026-09-05T18:15:00Z',
          settledAtUtc: '2026-09-05T18:40:00Z',
          chargedPriceCents: 200,
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
          state: 'handedOut',
          stateSetAtUtc: '2026-09-05T18:30:00Z',
          settledAtUtc: null,
          chargedPriceCents: null,
        },
      ],
    },
  ],
}

export function openItemsLaptop(
  report: unknown = TISCH_12,
  settlement: LaptopReply = answer(SETTLED),
): StubbedLaptop {
  return stubLaptop()
    .answers('GET', '/api/open-items/tables/table-12', answer(report))
    .answers('ANY', (call) => call.method !== 'GET', settlement)
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

export function orderCard(screen: MountedScreen, orderNumber: number): ScreenPart {
  return screen.get(`[data-test="order-card"][data-test-id="${orderNumber}"]`)
}

export function isHalfTaken(part: MountedScreen | ScreenPart, selector: string): boolean {
  return part.findComponent<typeof VCheckboxBtn>(selector).props('indeterminate') === true
}

export async function mountScreen(language: AppLanguage = 'de') {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.language = language
  useOpenItemsStore().openTable('table-12')
  const screen = mount(OpenItems, {
    global: { plugins: testPlugins(language) },
    attachTo: document.body,
  })
  await flushPromises()
  return screen
}
