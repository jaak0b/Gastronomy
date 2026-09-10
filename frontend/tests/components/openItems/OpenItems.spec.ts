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
      tableName: '12',
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

const TWO_TABLES = {
  ...OPEN_LIST,
  tables: [
    ...OPEN_LIST.tables,
    {
      tableName: '123',
      openAmountCents: 500,
      givenAwayAmountCents: 0,
      givenAwayItems: [],
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
  for (const title of screen.findAll('.open-table .v-expansion-panel-title')) {
    await title.trigger('click')
  }
  await flushPromises()
}

function lineOf(
  screen: Awaited<ReturnType<typeof mountScreen>>,
  tablePosition: number,
  linePosition: number,
) {
  return screen.findAll('.open-table')[tablePosition].findAll('.open-line')[linePosition]
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

  it('takes no tick from a second table, so one settlement can never span two tables', async () => {
    stubTheLaptopWith(TWO_TABLES, () => new Response(JSON.stringify(SETTLED), { status: 200 }))
    const screen = await mountScreen()
    await openEveryTable(screen)
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()

    await lineOf(screen, 1, 0).trigger('click')

    expect(openItems.selectedItemIds).toEqual(['item-1'])
  })

  it('offers no line of the table it is holding back, so nobody taps what does nothing', async () => {
    stubTheLaptopWith(TWO_TABLES, () => new Response(JSON.stringify(SETTLED), { status: 200 }))
    const screen = await mountScreen()
    await openEveryTable(screen)
    useOpenItemsStore().toggleItem('item-1')
    await screen.vm.$nextTick()

    expect(lineOf(screen, 0, 0).classes()).not.toContain('v-list-item--disabled')
    expect(lineOf(screen, 1, 0).classes()).toContain('v-list-item--disabled')
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

    await lineOf(screen, 1, 0).trigger('click')

    expect(openItems.selectedItemIds).toEqual(['item-7'])
  })

  it('settles what has been ticked at the price the phone showed', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    const openItems = useOpenItemsStore()
    openItems.toggleItem('item-1')
    await screen.vm.$nextTick()

    await screen.get('.settle').trigger('click')
    await flushPromises()

    expect(bodies[0]).toEqual({
      orderItemIds: ['item-1'],
      amountPaidCents: 350,
      paymentNotice: null,
    })
    expect(document.querySelector('.amount-paid-dialog')).toBeNull()
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

    await screen.get('.reload').trigger('click')
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
      'In den letzten 24 Stunden nicht kassiert: 4,00 €',
    )
    expect(screen.get('.given-away-reason').text()).toBe('Grund: Getraenk fuer die Kapelle')
    expect(screen.get('.given-away-price').text()).toBe('4,00 €')
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
    await screen.get('.settle-amount-paid').trigger('click')
    await flushPromises()
  }

  function fieldIn(selector: string): HTMLInputElement {
    return document.querySelector(`.amount-paid-dialog ${selector} input`) as HTMLInputElement
  }

  async function typeIn(selector: string, typed: string): Promise<void> {
    const field = fieldIn(selector)
    field.value = typed
    field.dispatchEvent(new Event('input'))
    await flushPromises()
  }

  async function pressConfirm(): Promise<void> {
    ;(document.querySelector('.amount-paid-dialog .confirm') as HTMLElement).click()
    await flushPromises()
  }

  it('offers the selection as the amount, so the ordinary case is one more tap', async () => {
    const screen = await mountScreen()

    await openTheAmountDialog(screen)

    expect(document.querySelector('.amount-paid-dialog .selected-total')?.textContent).toContain(
      'Ausgewählt: 3,50 €',
    )
    expect(fieldIn('.amount-field').value).toBe('3,50')
  })

  it('sends the smaller amount together with the typed reason', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('.amount-field', '2,00')
    await typeIn('.reason-field', 'Stammgast')
    await pressConfirm()

    expect(bodies[0]).toEqual({
      orderItemIds: ['item-1'],
      amountPaidCents: 200,
      paymentNotice: 'Stammgast',
    })
  })

  it('asks for a reason as soon as the amount falls short, and sends nothing until it is there', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('.amount-field', '2,00')
    await pressConfirm()

    expect(document.querySelector('.amount-paid-dialog .reason-field')).not.toBeNull()
    expect(
      document.querySelector('.amount-paid-dialog .confirm')?.hasAttribute('disabled'),
    ).toBe(true)
    expect(bodies).toEqual([])
  })

  it('sends nothing at all when the table was given the items, until a reason stands', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('.amount-field', '0')
    await typeIn('.reason-field', 'Essen fuer die Kapelle')
    await pressConfirm()

    expect(bodies[0]).toEqual({
      orderItemIds: ['item-1'],
      amountPaidCents: 0,
      paymentNotice: 'Essen fuer die Kapelle',
    })
  })

  it('needs no reason when the amount matches what the selection costs', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await pressConfirm()

    expect(document.querySelector('.amount-paid-dialog .reason-field')).toBeNull()
    expect(bodies[0]).toEqual({
      orderItemIds: ['item-1'],
      amountPaidCents: 350,
      paymentNotice: null,
    })
  })

  it('lets a guest round up without explaining themselves', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('.amount-field', '5,00')
    await pressConfirm()

    expect(bodies[0]).toEqual({
      orderItemIds: ['item-1'],
      amountPaidCents: 500,
      paymentNotice: null,
    })
  })

  it('sends nothing when the waiter backs out of the dialog', async () => {
    const { bodies } = stubTheLaptop()
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('.amount-field', '2,00')
    ;(document.querySelector('.amount-paid-dialog .cancel') as HTMLElement).click()
    await flushPromises()

    expect(bodies).toEqual([])
    expect(document.querySelector('.amount-paid-dialog')).toBeNull()
  })

  it('stops the reason where the laptop stops storing it', async () => {
    const screen = await mountScreen()
    await openTheAmountDialog(screen)

    await typeIn('.amount-field', '2,00')

    expect(fieldIn('.reason-field').getAttribute('maxlength')).toBe('200')
  })

  it('goes back to the list when the answer never came, because the list is where the reload is', async () => {
    const screen = await mountScreen()
    stubTheLaptopWith(OPEN_LIST, () => {
      throw new TypeError('the laptop cannot be reached')
    })
    await openTheAmountDialog(screen)

    await typeIn('.amount-field', '2,00')
    await typeIn('.reason-field', 'Stammgast')
    await pressConfirm()

    expect(document.querySelector('.amount-paid-dialog')).toBeNull()
    expect(screen.get('.settle-notice').text()).toBe(
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
            messageKey: 'openItems.settleFailed',
            parameters: {},
            details: null,
          }),
          { status: 400 },
        ),
    )
    await openTheAmountDialog(screen)

    await typeIn('.amount-field', '2,00')
    await typeIn('.reason-field', 'Stammgast')
    await pressConfirm()

    expect(fieldIn('.amount-field').value).toBe('2,00')
    expect(fieldIn('.reason-field').value).toBe('Stammgast')
    expect(document.querySelector('.amount-paid-dialog')?.textContent).toContain(
      'Das Abrechnen hat nicht geklappt.',
    )
  })
})
