import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OpenItems from '../../../src/phone/views/OpenItems.vue'
import { TABLE_LOOKUP_DEBOUNCE_MS } from '../../../src/phone/core/openItems'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { testPlugins } from '../../support/plugins'

const OPEN_LIST = {
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

const SETTLED = {
  settledOrderItemIds: ['item-1'],
  reappliedOrderItemIds: [],
  alreadySettledByOthersOrderItemIds: [],
}

const TWO_TABLES = {
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

const TABLE_REPORT = {
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

function stubTheLaptopWithALookup(
  report: unknown,
  list: unknown = OPEN_LIST,
): { bodies: unknown[] } {
  const bodies: unknown[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options: { body?: string }) => {
      if (options?.body !== undefined) {
        bodies.push(JSON.parse(options.body))
        return new Response(JSON.stringify(SETTLED), { status: 200 })
      }
      if (url.startsWith('/api/open-items/table?')) {
        return new Response(JSON.stringify(report), { status: 200 })
      }
      if (url === '/api/open-items/table-names') {
        return new Response(JSON.stringify({ tableNames: ['Tisch 12'] }), { status: 200 })
      }
      return new Response(JSON.stringify(list), { status: 200 })
    }),
  )
  return { bodies }
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

async function openEveryTable(screen: Awaited<ReturnType<typeof mountScreen>>): Promise<void> {
  for (const title of screen.findAll('[data-test="open-table-title"]')) {
    await title.trigger('click')
  }
  await flushPromises()
}

type MountedScreen = Awaited<ReturnType<typeof mountScreen>>
type ScreenPart = ReturnType<MountedScreen['get']>

function lineNamed(container: MountedScreen | ScreenPart, itemName: string): ScreenPart {
  const line = container
    .findAll('[data-test="open-line"]')
    .find((candidate) => candidate.get('[data-test="line-name"]').text() === itemName)
  if (line === undefined) {
    throw new Error(`No open line names ${itemName}.`)
  }
  return line
}

function lineOf(screen: MountedScreen, tableName: string, itemName: string): ScreenPart {
  return lineNamed(screen.get(`[data-test="open-table"][data-test-id="${tableName}"]`), itemName)
}

function lookupCard(screen: MountedScreen, orderNumber: number): ScreenPart {
  return screen.get(`[data-test="lookup-card"][data-test-id="${orderNumber}"]`)
}

function isHalfTaken(part: MountedScreen | ScreenPart, selector: string): boolean {
  return part.findComponent(selector).props('indeterminate') === true
}

async function mountScreen() {
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

    expect(screen.get('[data-test="open-table"] [data-test="table-name"]').text()).toBe('Tisch 12')
    expect(screen.get('[data-test="open-table"] [data-test="open-amount"]').text()).toBe('Offen: 7,00 €')
  })

  it('offers no settling action while the waiter has ticked nothing', async () => {
    const screen = await mountScreen()

    expect(screen.find('[data-test="settle-footer"]').exists()).toBe(false)
  })

  it('shows what has been ticked once the waiter takes a whole table', async () => {
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()

    openItems.setWholeTable(openItems.tables[0], true)
    await screen.vm.$nextTick()

    expect(screen.get('[data-test="selected-total"]').text()).toBe('Ausgewählt: 7,00 €')
  })

  it('keeps a table folded up until the waiter opens it, so the list stays readable', async () => {
    const screen = await mountScreen()

    expect(screen.findAll('[data-test="open-line"]')).toHaveLength(0)
  })

  it('ticks an item when the waiter taps its row, so the whole row is the target', async () => {
    const screen = await mountScreen()

    await screen.get('[data-test="open-table-title"]').trigger('click')
    await flushPromises()
    await lineOf(screen, '12', 'Bratwurst').trigger('click')

    expect(useOpenItemsStore().selectedItemIds).toEqual(['item-1'])
  })

  it('takes no tick from a second table, so one settlement can never span two tables', async () => {
    stubTheLaptopWith(TWO_TABLES, () => new Response(JSON.stringify(SETTLED), { status: 200 }))
    const screen = await mountScreen()
    await openEveryTable(screen)
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()

    await lineOf(screen, '123', 'Bier').trigger('click')

    expect(openItems.selectedItemIds).toEqual(['item-1'])
  })

  it('offers no line of the table it is holding back, so nobody taps what does nothing', async () => {
    stubTheLaptopWith(TWO_TABLES, () => new Response(JSON.stringify(SETTLED), { status: 200 }))
    const screen = await mountScreen()
    await openEveryTable(screen)
    useOpenItemsStore().toggleItem('item-1')
    await screen.vm.$nextTick()

    expect(lineOf(screen, '12', 'Bratwurst').classes()).not.toContain('v-list-item--disabled')
    expect(lineOf(screen, '123', 'Bier').classes()).toContain('v-list-item--disabled')
  })

  it('takes a tick from any table again once nothing is ticked', async () => {
    stubTheLaptopWith(TWO_TABLES, () => new Response(JSON.stringify(SETTLED), { status: 200 }))
    const screen = await mountScreen()
    await openEveryTable(screen)
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()

    await lineOf(screen, '123', 'Bier').trigger('click')

    expect(openItems.selectedItemIds).toEqual(['item-7'])
  })

  it('settles what has been ticked at the price the phone showed', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()

    await screen.get('[data-test="settle-in-cash"]').trigger('click')
    await flushPromises()

    expect(bodies[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
      paymentMethod: 'cash',
    })
    expect(document.querySelector('[data-test="amount-paid-dialog"]')).toBeNull()
  })

  it('fetches the list again when the waiter asks for it, which is what the notices name', async () => {
    let fetched = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        fetched += 1
        return new Response(JSON.stringify(OPEN_LIST), { status: 200 })
      }),
    )
    const screen = await mountScreen()
    const afterMounting = fetched

    await screen.get('[data-test="reload"]').trigger('click')
    await flushPromises()

    expect(fetched).toBe(afterMounting + 1)
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

    expect(screen.get('[data-test="empty"]').text()).toContain('Es ist nichts offen.')
  })

  it('claims nothing about the tables before the first list has arrived', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => {})))
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'

    const screen = mount(OpenItems, { global: { plugins: testPlugins() }, attachTo: document.body })
    await screen.vm.$nextTick()

    expect(screen.find('[data-test="empty"]').exists()).toBe(false)
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

    expect(screen.get('[data-test="list-incomplete"]').text()).toBe(
      'Fragen Sie am Tisch nach, was noch offen ist. Diese Liste zeigt 2 Positionen nicht, weil der Rechner die Bestellungen dazu nicht mehr findet.',
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
            reappliedOrderItemIds: [],
            alreadySettledByOthersOrderItemIds: ['item-2'],
          }),
          { status: 200 },
        ),
    )
    const openItems = useOpenItemsStore()
    openItems.setWholeTable(openItems.tables[0], true)
    await screen.vm.$nextTick()

    await screen.get('[data-test="settle-in-cash"]').trigger('click')
    await flushPromises()

    expect(screen.get('[data-test="settle-notice"]').text()).toBe(
      'Jemand anderes hatte 1 Position aus Ihrer Auswahl schon abgerechnet. Geben Sie dem Gast 3,50 € zurück.',
    )
  })
})

describe('settling what the table actually handed over', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function openTheAmountDialog(screen: Awaited<ReturnType<typeof mountScreen>>) {
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()
    await screen.get('[data-test="settle-amount-paid"]').trigger('click')
    await flushPromises()
  }

  function fieldIn(selector: string): HTMLInputElement {
    return document.querySelector(`[data-test="amount-paid-dialog"] ${selector} input`) as HTMLInputElement
  }

  async function typeIn(selector: string, typed: string): Promise<void> {
    const field = fieldIn(selector)
    field.value = typed
    field.dispatchEvent(new Event('input'))
    await flushPromises()
  }

  async function pressConfirm(button = '[data-test="confirm-in-cash"]'): Promise<void> {
    ;(document.querySelector(`[data-test="amount-paid-dialog"] ${button}`) as HTMLElement).click()
    await flushPromises()
  }

  it('offers the selection as the amount, so the ordinary case is one more tap', async () => {
    const screen = await mountScreen()

    await openTheAmountDialog(screen)

    expect(document.querySelector('[data-test="amount-paid-dialog"] [data-test="selected-total"]')?.textContent).toContain(
      'Ausgewählt: 3,50 €',
    )
    expect(fieldIn('[data-test="amount-field"]').value).toBe('3,50')
  })

  it('sends the smaller amount together with the typed reason', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('[data-test="amount-field"]', '2,00')
    await typeIn('[data-test="reason-field"]', 'Stammgast')
    await pressConfirm()

    expect(bodies[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 200, paymentNotice: 'Stammgast' }],
      paymentMethod: 'cash',
    })
  })

  it('asks for a reason as soon as the amount falls short, and sends nothing until it is there', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('[data-test="amount-field"]', '2,00')
    await pressConfirm()

    expect(document.querySelector('[data-test="amount-paid-dialog"] [data-test="reason-field"]')).not.toBeNull()
    expect(
      document.querySelector('[data-test="amount-paid-dialog"] [data-test="confirm-in-cash"]')?.hasAttribute('disabled'),
    ).toBe(true)
    expect(bodies).toEqual([])
  })

  it('sends nothing at all when the table was given the items, until a reason stands', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('[data-test="amount-field"]', '0')
    await typeIn('[data-test="reason-field"]', 'Essen fuer die Kapelle')
    await pressConfirm('[data-test="confirm-nothing-paid"]')

    expect(bodies[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 0, paymentNotice: 'Essen fuer die Kapelle' }],
      paymentMethod: 'none',
    })
  })

  it('needs no reason when the amount matches what the selection costs', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await pressConfirm()

    expect(document.querySelector('[data-test="amount-paid-dialog"] [data-test="reason-field"]')).toBeNull()
    expect(bodies[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
      paymentMethod: 'cash',
    })
  })

  it('lets a guest round up without explaining themselves', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('[data-test="amount-field"]', '5,00')
    await pressConfirm()

    expect(bodies[0]).toEqual({
      lines: [{ orderItemId: 'item-1', paidPriceCents: 500, paymentNotice: null }],
      paymentMethod: 'cash',
    })
  })

  it('sends nothing when the waiter backs out of the dialog', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('[data-test="amount-field"]', '2,00')
    ;(document.querySelector('[data-test="amount-paid-dialog"] [data-test="cancel"]') as HTMLElement).click()
    await flushPromises()

    expect(bodies).toEqual([])
    expect(document.querySelector('[data-test="amount-paid-dialog"]')).toBeNull()
  })

  it('stops the reason where the laptop stops storing it', async () => {
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('[data-test="amount-field"]', '2,00')

    expect(fieldIn('[data-test="reason-field"]').getAttribute('maxlength')).toBe('200')
  })

  it('goes back to the list when the answer never came, because the list is where the reload is', async () => {
    const screen = await mountScreen()
    stubTheLaptopWith(OPEN_LIST, () => {
      throw new TypeError('the laptop cannot be reached')
    })
    await openTheAmountDialog(screen)

    await typeIn('[data-test="amount-field"]', '2,00')
    await typeIn('[data-test="reason-field"]', 'Stammgast')
    await pressConfirm()

    expect(document.querySelector('[data-test="amount-paid-dialog"]')).toBeNull()
    expect(screen.get('[data-test="settle-notice"]').text()).toBe(
      'Es ist nicht klar, ob die Abrechnung angekommen ist. Laden Sie die Liste neu und schauen Sie nach, ob die Positionen noch offen sind.',
    )
    expect(useOpenItemsStore().selectedItemIds).toEqual(['item-1'])
  })

  it('keeps what was typed on screen when the laptop refuses, so nobody types it twice', async () => {
    const screen = await mountScreen()
    stubTheLaptopWith(
      OPEN_LIST,
      () =>
        new Response(
          JSON.stringify({
            code: 'ValidationFailed',
            messageKey: 'phone.openItems.errors.settleFailed',
            parameters: {},
            details: null,
          }),
          { status: 400 },
        ),
    )
    await openTheAmountDialog(screen)

    await typeIn('[data-test="amount-field"]', '2,00')
    await typeIn('[data-test="reason-field"]', 'Stammgast')
    await pressConfirm()

    expect(fieldIn('[data-test="amount-field"]').value).toBe('2,00')
    expect(fieldIn('[data-test="reason-field"]').value).toBe('Stammgast')
    expect(document.querySelector('[data-test="amount-paid-dialog"]')?.textContent).toContain(
      'Versuchen Sie es noch einmal. Das Abrechnen ist fehlgeschlagen.',
    )
  })
})

describe('looking up one table from the screen that shows what is open', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  async function mountTheScreenWithALookup(
    list: unknown = OPEN_LIST,
    report: unknown = TABLE_REPORT,
  ) {
    const { bodies } = stubTheLaptopWithALookup(report, list)
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'
    const screen = mount(OpenItems, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await flushPromises()
    vi.useFakeTimers()
    return { screen, bodies }
  }

  async function typeTheTableName(
    screen: Awaited<ReturnType<typeof mountTheScreenWithALookup>>['screen'],
    name: string,
  ): Promise<void> {
    await screen.get('[data-test="table-field"] input').setValue(name)
    await vi.advanceTimersByTimeAsync(TABLE_LOOKUP_DEBOUNCE_MS)
    await screen.vm.$nextTick()
  }

  async function arriveFromAnOrderSentToBeSettled(itemIds: string[]) {
    stubTheLaptopWithALookup(TABLE_REPORT)
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'
    useOpenItemsStore().openTableAndSelectItemsOnceLoaded('Tisch 12', itemIds)
    const screen = mount(OpenItems, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await flushPromises()
    return screen
  }

  function tickOf(screen: Awaited<ReturnType<typeof mountTheScreenWithALookup>>['screen'], orderNumber: number) {
    return lookupCard(screen, orderNumber)
      .findAll('[data-test="line-tick"] input')
      .map((tick) => (tick.element as HTMLInputElement).checked)
  }

  it('shows the table of the order just sent in the search field', async () => {
    const screen = await arriveFromAnOrderSentToBeSettled(['item-half', 'item-waiting', 'item-settled'])

    expect((screen.get('[data-test="table-field"] input').element as HTMLInputElement).value).toBe('Tisch 12')
  })

  it('ticks the open items of the order just sent and leaves the older order of the table unticked', async () => {
    const screen = await arriveFromAnOrderSentToBeSettled(['item-half', 'item-waiting', 'item-settled'])

    expect({ older: tickOf(screen, 137), justSent: tickOf(screen, 138) }).toEqual({
      older: [false],
      justSent: [true, true],
    })
  })

  it('starts with an empty search when the screen is opened again later', async () => {
    const first = await arriveFromAnOrderSentToBeSettled(['item-waiting'])
    first.unmount()

    const again = mount(OpenItems, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()

    expect({
      typed: (again.get('[data-test="table-field"] input').element as HTMLInputElement).value,
      ticked: useOpenItemsStore().selectedItemIds,
    }).toEqual({ typed: '', ticked: [] })
  })

  it('hides the list and its notices while the lookup shows the orders of a table', async () => {
    const { screen } = await mountTheScreenWithALookup({
      ...OPEN_LIST,
      tables: [],
      itemsWithoutAnOrderCount: 2,
    })
    expect(screen.find('[data-test="empty"]').exists()).toBe(true)
    expect(screen.find('[data-test="list-incomplete"]').exists()).toBe(true)

    await typeTheTableName(screen, 'Tisch 12')

    expect(screen.find('[data-test="empty"]').exists()).toBe(false)
    expect(screen.find('[data-test="list-incomplete"]').exists()).toBe(false)
    expect(screen.find('[data-test="open-table"]').exists()).toBe(false)
    expect(screen.findAll('[data-test="lookup-card"]')).toHaveLength(3)
  })

  it('brings the list back when the waiter clears the table name', async () => {
    const { screen } = await mountTheScreenWithALookup()
    await typeTheTableName(screen, 'Tisch 12')
    expect(screen.findAll('[data-test="lookup-card"]')).toHaveLength(3)

    await screen.get('[data-test="table-field"] input').setValue('')
    await screen.vm.$nextTick()

    expect(screen.findAll('[data-test="lookup-card"]')).toHaveLength(0)
    expect(screen.find('[data-test="open-table"]').exists()).toBe(true)
  })

  it('colours each order by how far its positions have been produced', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')

    expect([137, 138, 139].map((orderNumber) => lookupCard(screen, orderNumber).attributes('data-state'))).toEqual([
      'none',
      'some',
      'all',
    ])
  })

  it('marks each position as produced or not, with an icon beside the state', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')

    const notProduced = lineNamed(lookupCard(screen, 137), 'Bratwurst')
    expect(notProduced.classes()).toContain('is-not-produced')
    expect(notProduced.get('[data-test="line-state"]').classes()).toContain('mdi-clock-outline')

    const produced = lineNamed(lookupCard(screen, 139), 'Kuchen')
    expect(produced.classes()).toContain('is-produced')
    expect(produced.get('[data-test="line-state"]').classes()).toContain('mdi-check')
  })

  it('writes the word for paid on a settled position and leaves out its tick box', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')

    const settled = lineNamed(lookupCard(screen, 138), 'Wasser')
    expect(settled.get('[data-test="line-paid"]').text()).toBe('Bezahlt')
    expect(settled.find('[data-test="line-tick"]').exists()).toBe(false)
  })

  it('settles a ticked position through the same footer as the list', async () => {
    const { screen, bodies } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')
    await lineNamed(lookupCard(screen, 137), 'Bratwurst').trigger('click')
    await screen.vm.$nextTick()
    expect(screen.get('[data-test="selected-total"]').text()).toBe('Ausgewählt: 3,50 €')

    await screen.get('[data-test="settle-in-cash"]').trigger('click')
    await vi.advanceTimersByTimeAsync(0)

    expect(bodies[0]).toEqual({
      lines: [{ orderItemId: 'item-plain', paidPriceCents: 350, paymentNotice: null }],
      paymentMethod: 'cash',
    })
  })

  it('takes the whole table at once across its orders', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')
    await screen.get('[data-test="whole-table"] input').trigger('click')
    await vi.advanceTimersByTimeAsync(0)

    expect(screen.get('[data-test="selected-total"]').text()).toBe('Ausgewählt: 12,00 €')
  })

  it('shows the whole table as half taken while only some of its items are ticked', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')
    await lineNamed(lookupCard(screen, 137), 'Bratwurst').trigger('click')
    await screen.vm.$nextTick()

    expect(isHalfTaken(screen, '[data-test="whole-table"]')).toBe(true)
  })

  it('ticks every open item of one order from its header and leaves the other orders alone', async () => {
    const { screen } = await mountTheScreenWithALookup()

    await typeTheTableName(screen, 'Tisch 12')
    await lookupCard(screen, 138).get('[data-test="whole-order"] input').trigger('click')
    await screen.vm.$nextTick()

    expect({ first: tickOf(screen, 137), second: tickOf(screen, 138), third: tickOf(screen, 139) }).toEqual({
      first: [false],
      second: [true, true],
      third: [false],
    })
  })

  it('shows an order as half taken after one of its items is unticked again', async () => {
    const { screen } = await mountTheScreenWithALookup()
    await typeTheTableName(screen, 'Tisch 12')
    const card = lookupCard(screen, 138)
    await card.get('[data-test="whole-order"] input').trigger('click')
    await screen.vm.$nextTick()

    await lineNamed(card, 'Bier').trigger('click')
    await screen.vm.$nextTick()

    expect(isHalfTaken(card, '[data-test="whole-order"]')).toBe(true)
  })

  it('ticks the rest of a half taken order when its header is tapped', async () => {
    const { screen } = await mountTheScreenWithALookup()
    await typeTheTableName(screen, 'Tisch 12')
    await lineNamed(lookupCard(screen, 138), 'Bier').trigger('click')
    await screen.vm.$nextTick()

    await lookupCard(screen, 138).get('[data-test="whole-order"] input').trigger('click')
    await screen.vm.$nextTick()

    expect(tickOf(screen, 138)).toEqual([true, true])
  })

  it('offers no header tick on an order whose items are all settled', async () => {
    const paidOrder = {
      ...TABLE_REPORT.orders[1],
      items: [TABLE_REPORT.orders[1].items[2]],
    }
    const { screen } = await mountTheScreenWithALookup(OPEN_LIST, {
      ...TABLE_REPORT,
      orders: [TABLE_REPORT.orders[0], paidOrder],
    })

    await typeTheTableName(screen, 'Tisch 12')

    const cards = screen.findAll('[data-test="lookup-card"]')
    expect({
      open: cards[0].find('[data-test="whole-order"]').exists(),
      paid: cards[1].find('[data-test="whole-order"]').exists(),
    }).toEqual({ open: true, paid: false })
  })

  it('says that the table has no orders at all when the lookup finds none', async () => {
    const { screen } = await mountTheScreenWithALookup(OPEN_LIST, {
      tableName: 'Tisch 99',
      openAmountCents: 0,
      orders: [],
    })

    await typeTheTableName(screen, 'Tisch 99')

    expect(screen.get('[data-test="lookup-empty"]').text()).toBe(
      'Für diesen Tisch gibt es keine Bestellungen.',
    )
    expect(screen.find('[data-test="open-table"]').exists()).toBe(false)
  })

  it('says the laptop could not be reached and loads the lookup again from the reload button', async () => {
    let lookups = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (url.startsWith('/api/open-items/table?')) {
          lookups += 1
          return new Response('{}', { status: 500 })
        }
        if (url === '/api/open-items/table-names') {
          return new Response(JSON.stringify({ tableNames: [] }), { status: 200 })
        }
        return new Response(JSON.stringify(OPEN_LIST), { status: 200 })
      }),
    )
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'
    const screen = mount(OpenItems, {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await flushPromises()
    vi.useFakeTimers()

    await typeTheTableName(screen, 'Tisch 12')

    expect(screen.get('[data-test="load-failed"]').text()).toBe(
      'Tippen Sie auf "Liste neu laden". Der Rechner war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    )
    expect(screen.find('[data-test="open-table"]').exists()).toBe(false)

    await screen.get('[data-test="reload"]').trigger('click')
    await vi.advanceTimersByTimeAsync(0)

    expect(lookups).toBe(2)
  })
})

describe('an order sent to be settled whose answer arrives while the waiter already looks up another table', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
    document.body.innerHTML = ''
  })

  function stubTheLaptopAnsweringTheOrderLate(): { answerTheOrder: () => void } {
    let answerTheOrder: () => void = () => {}
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (url === '/api/orders') {
          return new Promise<Response>((resolve) => {
            answerTheOrder = () =>
              resolve(
                new Response(
                  JSON.stringify({
                    orderId: 'order-12',
                    globalOrderNumber: 150,
                    status: 'open',
                    totalCents: 200,
                    createdAtUtc: '2026-09-05T18:50:00Z',
                    stationOrders: [
                      {
                        stationOrderId: 'station-order-12',
                        stationId: 'station-bar',
                        stationName: 'Bar',
                        stationOrderNumber: 9,
                        deliveryMode: 'together',
                        itemIds: ['new-wasser'],
                      },
                    ],
                  }),
                  { status: 200 },
                ),
              )
          })
        }
        if (url.startsWith('/api/open-items/table?')) {
          return new Response(JSON.stringify(TABLE_REPORT), { status: 200 })
        }
        if (url === '/api/open-items/table-names') {
          return new Response(JSON.stringify({ tableNames: ['Tisch 3'] }), { status: 200 })
        }
        return new Response(JSON.stringify(OPEN_LIST), { status: 200 })
      }),
    )
    return { answerTheOrder: () => answerTheOrder() }
  }

  function anOrderForTableTwelve() {
    useCatalogStore().catalog = {
      categories: [
        { categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#C62828', sortOrder: 1 },
      ],
      items: [
        {
          id: 'item-wasser',
          name: 'Wasser',
          categoryId: 'category-getraenke',
          priceCents: 200,
          sortOrder: 1,
          isAvailable: true,
          stationIds: ['station-bar'],
          productionMinutes: 0,
          isQueueIndependent: false,
        },
      ],
      stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
    }
    const order = useOrderStore()
    order.addItem({ catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser' })
    order.setTable('Tisch 12')
    return order
  }

  it('keeps the table the waiter typed and the item the waiter ticked', async () => {
    const { answerTheOrder } = stubTheLaptopAnsweringTheOrderLate()
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = 'de'
    const order = anOrderForTableTwelve()
    const sending = order.send('settleRightAway')
    const screen = mount(OpenItems, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()
    await screen.get('[data-test="table-field"] input').setValue('Tisch 3')
    await new Promise((resolve) => setTimeout(resolve, TABLE_LOOKUP_DEBOUNCE_MS))
    await flushPromises()
    await lineNamed(lookupCard(screen, 137), 'Bratwurst').trigger('click')

    answerTheOrder()
    await sending
    await flushPromises()

    const openItems = useOpenItemsStore()
    expect({
      lookup: openItems.lookupName,
      field: (screen.get('[data-test="table-field"] input').element as HTMLInputElement).value,
      ticked: openItems.selectedItemIds,
    }).toEqual({ lookup: 'Tisch 3', field: 'Tisch 3', ticked: ['item-plain'] })
  })
})

describe('saying how the table paid with the button that settles', () => {
  const ONE_FREE_ITEM = {
    tables: [
      {
        tableName: '12',
        openAmountCents: 0,
        items: [
          {
            orderItemId: 'item-water',
            orderId: 'order-1',
            globalOrderNumber: 137,
            itemName: 'Leitungswasser',
            note: null,
            unitPriceCents: 0,
            orderedAtUtc: '2026-09-05T18:00:00Z',
          },
        ],
      },
    ],
    itemsWithoutAnOrderCount: 0,
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function mountScreenIn(language: 'de' | 'en') {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = language
    const screen = mount(OpenItems, {
      global: { plugins: testPlugins(language) },
      attachTo: document.body,
    })
    await flushPromises()
    return screen
  }

  function footerButtons(screen: Awaited<ReturnType<typeof mountScreen>>): string[] {
    return screen.get('[data-test="settle-footer"]').findAll('button').map((button) => button.text())
  }

  it('offers cash, card and another amount once something with a price is ticked', async () => {
    stubTheLaptop()
    const screen = await mountScreenIn('de')
    useOpenItemsStore().toggleItem('item-1')
    await screen.vm.$nextTick()

    expect(footerButtons(screen)).toEqual([
      'Bar abrechnen',
      'Mit Karte abrechnen',
      'Anderen Betrag abrechnen',
    ])
  })

  it('offers the same buttons in English', async () => {
    stubTheLaptop()
    const screen = await mountScreenIn('en')
    useOpenItemsStore().toggleItem('item-1')
    await screen.vm.$nextTick()

    expect(footerButtons(screen)).toEqual([
      'Settle in cash',
      'Settle by card',
      'Settle a different amount',
    ])
  })

  it('settles at full price by card when the waiter taps card', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreenIn('de')
    useOpenItemsStore().toggleItem('item-1')
    await screen.vm.$nextTick()

    await screen.get('[data-test="settle-by-card"]').trigger('click')
    await flushPromises()

    expect(bodies).toEqual([
      {
        lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
        paymentMethod: 'card',
      },
    ])
  })

  it('offers one settle button that says nothing was paid when only free items are ticked', async () => {
    const { bodies } = stubTheLaptopWith(
      ONE_FREE_ITEM,
      () => new Response(JSON.stringify(SETTLED), { status: 200 }),
    )
    const screen = await mountScreenIn('de')
    useOpenItemsStore().toggleItem('item-water')
    await screen.vm.$nextTick()

    expect(footerButtons(screen)).toEqual(['Abrechnen', 'Anderen Betrag abrechnen'])
    await screen.get('[data-test="settle-nothing-paid"]').trigger('click')
    await flushPromises()

    expect(bodies).toEqual([
      {
        lines: [{ orderItemId: 'item-water', paidPriceCents: 0, paymentNotice: null }],
        paymentMethod: 'none',
      },
    ])
  })

  it('keeps every settle button shut while a settlement is on its way', async () => {
    stubTheLaptopWith(OPEN_LIST, () => new Response(JSON.stringify(SETTLED), { status: 200 }))
    const screen = await mountScreenIn('de')
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => {})))
    useOpenItemsStore().toggleItem('item-1')
    await screen.vm.$nextTick()

    await screen.get('[data-test="settle-in-cash"]').trigger('click')
    await screen.vm.$nextTick()

    const shut = screen
      .get('[data-test="settle-footer"]')
      .findAll('button')
      .map((button) => button.attributes('disabled'))
    expect(shut).toEqual(['', '', ''])
  })

  it('sends card from the amount dialog when the waiter taps card there', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreenIn('de')
    useOpenItemsStore().toggleItem('item-1')
    await screen.vm.$nextTick()
    await screen.get('[data-test="settle-amount-paid"]').trigger('click')
    await flushPromises()

    ;(document.querySelector('[data-test="amount-paid-dialog"] [data-test="confirm-by-card"]') as HTMLElement).click()
    await flushPromises()

    expect(bodies).toEqual([
      {
        lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
        paymentMethod: 'card',
      },
    ])
  })
})