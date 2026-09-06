import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OpenItems from '../../../src/views/OpenItems.vue'
import { useOpenItemsStore } from '../../../src/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/stores/session'
import { testPlugins } from '../../support/plugins'

const OPEN_LIST = {
  tables: [
    {
      tableName: 'Tisch 12',
      openAmountCents: 700,
      givenAwayAmountCents: 0,
      givenAwayItems: [],
      items: [
        {
          orderItemId: 'item-1',
          orderId: 'order-1',
          globalOrderNumber: 137,
          itemName: 'Bratwurst',
          note: 'ohne Zwiebeln',
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:00:00Z',
          stationName: 'Küche',
          deliveryMode: 'together',
          productionStatus: 'inProduction',
        },
        {
          orderItemId: 'item-2',
          orderId: 'order-1',
          globalOrderNumber: 137,
          itemName: 'Bier',
          note: null,
          unitPriceCents: 350,
          orderedAtUtc: '2026-09-05T18:00:00Z',
          stationName: 'Theke',
          deliveryMode: 'asItComes',
          productionStatus: 'waiting',
        },
      ],
    },
  ],
  itemsWithoutAnOrderCount: 0,
}

const GIVEN_AWAY_LIST = {
  tables: [
    {
      tableName: 'Tisch 5',
      openAmountCents: 0,
      givenAwayAmountCents: 400,
      items: [],
      givenAwayItems: [
        {
          orderItemId: 'item-9',
          orderId: 'order-9',
          globalOrderNumber: 140,
          itemName: 'Bier',
          waivedAmountCents: 400,
          paymentNotice: 'Getraenk fuer die Kapelle',
          settledAtUtc: '2026-09-05T19:00:00Z',
        },
      ],
    },
  ],
  itemsWithoutAnOrderCount: 0,
}

const SETTLED = {
  settledOrderItemIds: ['item-1'],
  alreadySettledOrderItemIds: [],
  otherPhonesWereTold: true,
}

function stubTheLaptopWith(list: unknown, settlement: () => Response): { bodies: unknown[] } {
  const bodies: unknown[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options: { body?: string }) => {
      if (options?.body !== undefined) {
        bodies.push(JSON.parse(options.body))
        return settlement()
      }
      return new Response(JSON.stringify(list), { status: 200 })
    }),
  )
  return { bodies }
}

function stubTheLaptop(): { bodies: unknown[] } {
  return stubTheLaptopWith(
    OPEN_LIST,
    () => new Response(JSON.stringify(SETTLED), { status: 200 }),
  )
}

async function mountScreen() {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
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

describe('the screen that shows what the tables still owe', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names every table with something open and what it still owes', async () => {
    const screen = await mountScreen()

    expect(screen.get('.open-table .table-name').text()).toBe('Tisch 12')
    expect(screen.get('.open-table .open-amount').text()).toBe('Offen: 7,00 €')
  })

  it('offers no settling action while the waiter has ticked nothing', async () => {
    const screen = await mountScreen()

    expect(screen.find('.settle-footer').exists()).toBe(false)
  })

  it('shows what has been ticked once the waiter takes a whole table', async () => {
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()

    openItems.setWholeTable(openItems.tables[0], true)
    await screen.vm.$nextTick()

    expect(screen.get('.selected-total').text()).toBe('Ausgewählt: 7,00 €')
  })

  it('keeps a table folded up until the waiter opens it, so the list stays readable', async () => {
    const screen = await mountScreen()

    expect(screen.findAll('.open-line')).toHaveLength(0)
  })

  it('ticks an item when the waiter taps its row, so the whole row is the target', async () => {
    const screen = await mountScreen()

    await screen.get('.open-table .v-expansion-panel-title').trigger('click')
    await flushPromises()
    await screen.findAll('.open-line')[0].trigger('click')

    expect(useOpenItemsStore().selectedItemIds).toEqual(['item-1'])
  })

  it('settles what has been ticked at the price the phone showed', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()

    await screen.get('.settle').trigger('click')
    await flushPromises()

    expect(bodies[0]).toEqual({ orderItemIds: ['item-1'] })
  })

  it('says that every table is settled when nothing is open', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ tables: [], itemsWithoutAnOrderCount: 0 }), {
            status: 200,
          }),
      ),
    )
    const screen = await mountScreen()

    expect(screen.get('.empty').text()).toContain('Es ist nichts offen.')
  })

  it('claims nothing about the tables before the first list has arrived', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => {})))
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'

    const screen = mount(OpenItems, { global: { plugins: testPlugins() }, attachTo: document.body })
    await screen.vm.$nextTick()

    expect(screen.find('.empty').exists()).toBe(false)
  })

  it('says that the list is short of items the laptop cannot trace back to an order', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ tables: [], itemsWithoutAnOrderCount: 2 }), {
            status: 200,
          }),
      ),
    )
    const screen = await mountScreen()

    expect(screen.get('.list-incomplete').text()).toBe(
      'Fragen Sie am Tisch nach, was noch offen ist. Diese Liste zeigt 2 Positionen nicht, weil der Laptop die Bestellungen dazu nicht mehr findet.',
    )
  })

  it('warns that somebody else had already settled part of the selection', async () => {
    const screen = await mountScreen()
    stubTheLaptopWith(
      OPEN_LIST,
      () =>
        new Response(
          JSON.stringify({
            settledOrderItemIds: ['item-1'],
            alreadySettledOrderItemIds: ['item-2'],
            otherPhonesWereTold: true,
          }),
          { status: 200 },
        ),
    )
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()

    await screen.get('.settle').trigger('click')
    await flushPromises()

    expect(screen.get('.settle-notice').text()).toBe(
      '1 Position aus Ihrer Auswahl hatte jemand anderes schon abgerechnet. Prüfen Sie, ob Sie dafür Geld genommen haben, und geben Sie es dem Gast zurück.',
    )
  })

  it('keeps a table that only has items given away, so the record stays visible', async () => {
    stubTheLaptopWith(
      GIVEN_AWAY_LIST,
      () => new Response(JSON.stringify(SETTLED), { status: 200 }),
    )
    const screen = await mountScreen()

    await screen.get('.open-table .v-expansion-panel-title').trigger('click')
    await flushPromises()

    expect(screen.get('.given-away-heading').text()).toBe(
      'In den letzten 24 Stunden kostenlos abgegeben: 4,00 €',
    )
    expect(screen.get('.given-away-reason').text()).toBe('Grund: Getraenk fuer die Kapelle')
    expect(screen.get('.given-away-price').text()).toBe('4,00 €')
  })
})

describe('giving food and drink away', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks for a reason before anything is settled at zero', async () => {
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()

    await screen.get('.settle-free-of-charge').trigger('click')
    await flushPromises()

    expect(document.querySelector('.free-of-charge-dialog .reason-field')).not.toBeNull()
  })

  it('refuses to settle at zero while the reason is empty, and says so at the field', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()
    await screen.get('.settle-free-of-charge').trigger('click')
    await flushPromises()

    ;(document.querySelector('.free-of-charge-dialog .confirm') as HTMLElement).click()
    await flushPromises()

    expect(bodies).toEqual([])
    expect(document.querySelector('.free-of-charge-dialog')?.textContent).toContain(
      'Tragen Sie einen Grund ein, bevor Sie kostenlos abrechnen.',
    )
  })

  it('sends the typed reason with the items that are given away', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()
    await screen.get('.settle-free-of-charge').trigger('click')
    await flushPromises()

    const field = document.querySelector('.free-of-charge-dialog .reason-field input')
    const typed = field as HTMLInputElement
    typed.value = 'Essen fuer die Kapelle'
    typed.dispatchEvent(new Event('input'))
    await flushPromises()
    ;(document.querySelector('.free-of-charge-dialog .confirm') as HTMLElement).click()
    await flushPromises()

    expect(bodies[0]).toEqual({
      orderItemIds: ['item-1'],
      paymentNotice: 'Essen fuer die Kapelle',
    })
  })

  it('keeps the typed reason on screen when the laptop refuses, so nobody retypes it', async () => {
    const screen = await mountScreen()
    stubTheLaptopWith(
      OPEN_LIST,
      () =>
        new Response(
          JSON.stringify({
            code: 'ValidationFailed',
            messageKey: 'openItems.settleFailed',
            parameters: {},
            details: null,
          }),
          { status: 400 },
        ),
    )
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()
    await screen.get('.settle-free-of-charge').trigger('click')
    await flushPromises()
    const typed = document.querySelector(
      '.free-of-charge-dialog .reason-field input',
    ) as HTMLInputElement
    typed.value = 'Essen fuer die Kapelle'
    typed.dispatchEvent(new Event('input'))
    await flushPromises()

    ;(document.querySelector('.free-of-charge-dialog .confirm') as HTMLElement).click()
    await flushPromises()

    const dialog = document.querySelector('.free-of-charge-dialog')
    expect(
      (dialog?.querySelector('.reason-field input') as HTMLInputElement).value,
    ).toBe('Essen fuer die Kapelle')
    expect(dialog?.textContent).toContain('Das Abrechnen hat nicht geklappt.')
  })
})

describe('what an open item says about where it stands', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function openedTable() {
    const screen = await mountScreen()
    await screen.get('.open-table .v-expansion-panel-title').trigger('click')
    await flushPromises()
    return screen
  }

  it('names the station and says the item is being prepared there', async () => {
    const screen = await openedTable()

    expect(screen.findAll('.open-line .line-production')[0].text()).toBe(
      'In Zubereitung bei Küche.',
    )
  })

  it('says an item nobody has started yet is still waiting', async () => {
    const screen = await openedTable()

    expect(screen.findAll('.open-line .line-production')[1].text()).toBe('Wartet bei Theke.')
  })

  it('says whether the item comes with the rest of the order or on its own', async () => {
    const screen = await openedTable()

    expect(screen.findAll('.open-line .line-delivery').map((line) => line.text())).toEqual([
      'Kommt zusammen mit dem Rest der Bestellung.',
      'Kommt, sobald es fertig ist.',
    ])
  })
})
