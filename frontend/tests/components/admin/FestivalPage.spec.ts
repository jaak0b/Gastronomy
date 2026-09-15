import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VAutocomplete, VCheckbox } from 'vuetify/components'
import FestivalPage from '../../../src/components/admin/festivals/FestivalPage.vue'
import FestivalPlacementDialog from '../../../src/components/admin/festivals/FestivalPlacementDialog.vue'
import StationSelect from '../../../src/components/admin/festivals/StationSelect.vue'
import type { AdminItem } from '../../../src/stores/admin/items'
import { useConnectionStore } from '../../../src/stores/connection'
import { pressInDialog, testPlugins, waitForDialog } from '../../support/plugins'

const FESTIVAL_ID = 'fest-1'
const KITCHEN_ID = 'station-kueche'
const BAR_ID = 'station-theke'
const SAUSAGE_ID = 'item-bratwurst'
const BEER_ID = 'item-bier'
const FOOD_ID = 'category-speisen'

const SUMMER = {
  festivalId: FESTIVAL_ID,
  name: 'Sommerfest',
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-19T02:00:00Z',
  isHidden: false,
  isRunning: true,
  stationCount: 1,
  menuItemCount: 1,
  orderCount: 0,
}

const KITCHEN = {
  stationId: KITCHEN_ID,
  name: 'Küche',
  sortOrder: 1,
  isActive: true,
  hasDevice: true,
  isAtTheFestival: true,
}

const BAR = {
  stationId: BAR_ID,
  name: 'Theke',
  sortOrder: 2,
  isActive: true,
  hasDevice: false,
  isAtTheFestival: false,
}

const FOOD = {
  categoryId: FOOD_ID,
  name: 'Speisen',
  colourHex: '#FFEB3B',
  sortOrder: 1,
  isActive: true,
}

const SAUSAGE = {
  itemId: SAUSAGE_ID,
  name: 'Bratwurst',
  categoryId: FOOD_ID,
  sortOrder: 1,
  isActive: true,
  productionMinutes: null,
  atTheFestival: { priceCents: 350, isAvailable: true, stationIds: [KITCHEN_ID] },
}

const DRINKS_ID = 'category-getraenke'

const DRINKS = {
  categoryId: DRINKS_ID,
  name: 'Getränke',
  colourHex: '#90CAF9',
  sortOrder: 2,
  isActive: true,
}

const BEER = {
  itemId: BEER_ID,
  name: 'Bier',
  categoryId: FOOD_ID,
  sortOrder: 2,
  isActive: true,
  productionMinutes: null,
  atTheFestival: null,
}

interface Call {
  url: string
  method: string
  body: unknown
}

interface Laptop {
  festivals?: unknown[]
  stations?: unknown[]
  categories?: unknown[]
  items?: unknown[]
  refusal?: { status: number; body: unknown; method: string }
  refuses?: (call: Call) => { status: number; body: unknown } | null
  created?: Record<string, unknown>
  waitBeforeAnswering?: (call: Call) => Promise<void>
}

function stubLaptop(laptop: Laptop = {}): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      const call = {
        url,
        method,
        body: init?.body === undefined ? null : JSON.parse(String(init.body)),
      }
      calls.push(call)
      if (laptop.waitBeforeAnswering !== undefined) {
        await laptop.waitBeforeAnswering(call)
      }
      const byRequest = laptop.refuses?.(call) ?? null
      const byMethod =
        laptop.refusal !== undefined && method === laptop.refusal.method ? laptop.refusal : null
      const refusal = byRequest ?? byMethod
      if (refusal !== null) {
        return new Response(JSON.stringify(refusal.body), { status: refusal.status })
      }
      if (method !== 'GET') {
        return new Response(JSON.stringify(laptop.created ?? {}), { status: 200 })
      }
      if (url.startsWith('/api/admin/festivals')) {
        return new Response(JSON.stringify({ festivals: laptop.festivals ?? [SUMMER] }), {
          status: 200,
        })
      }
      if (url.startsWith('/api/admin/stations')) {
        return new Response(JSON.stringify({ stations: laptop.stations ?? [KITCHEN, BAR] }), {
          status: 200,
        })
      }
      if (url.startsWith('/api/admin/categories')) {
        return new Response(JSON.stringify({ categories: laptop.categories ?? [FOOD] }), {
          status: 200,
        })
      }
      return new Response(JSON.stringify({ items: laptop.items ?? [SAUSAGE, BEER] }), {
        status: 200,
      })
    }),
  )
  return calls
}

function mountPage(festivalId = FESTIVAL_ID) {
  return mount(FestivalPage, {
    props: { festivalId },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

function inDialog(selector: string): HTMLElement {
  return document.querySelector(selector) as HTMLElement
}

function writeInto(selector: string, value: string): void {
  const field = inDialog(selector) as HTMLInputElement
  field.value = value
  field.dispatchEvent(new Event('input'))
}

function writtenCalls(calls: Call[]): Call[] {
  return calls.filter((call) => call.method !== 'GET')
}

async function openPlacementDialog(page: VueWrapper): Promise<void> {
  await page.findAllComponents(VAutocomplete)[1].setValue(BEER_ID)
  await page.get('.add-item').trigger('click')
  await vi.waitFor(() => expect(document.querySelector('.placement-dialog')).not.toBeNull())
}

async function openRowStations(page: VueWrapper, rowIndex = 0): Promise<VueWrapper[]> {
  await page.findAll('.festival-item-row')[rowIndex].get('.station-select').trigger('click')
  const select = page.findAllComponents(StationSelect)[rowIndex]
  await vi.waitFor(() => expect(select.findAllComponents(VCheckbox).length).toBeGreaterThan(0))
  return select.findAllComponents(VCheckbox)
}

async function openDialogStations(page: VueWrapper): Promise<VueWrapper[]> {
  inDialog('.placement-dialog .station-select').click()
  const select = page.findComponent(FestivalPlacementDialog).findComponent(StationSelect)
  await vi.waitFor(() => expect(select.findAllComponents(VCheckbox).length).toBeGreaterThan(0))
  return select.findAllComponents(VCheckbox)
}

function listedItems(): AdminItem[] {
  return [
    {
      ...SAUSAGE,
      atTheFestival: { ...SAUSAGE.atTheFestival, stationIds: [...SAUSAGE.atTheFestival.stationIds] },
    },
    { ...BEER },
  ]
}

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the name and the dates at the top of the page', () => {
  it('carry what the laptop knows about this festival', async () => {
    stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-name').exists()).toBe(true))

    expect(page.get('.festival-name').text()).toBe('Sommerfest')
    expect(page.get('.running').text()).toBe('Aktiv')
    expect((page.get('.festival-name-field input').element as HTMLInputElement).value).toBe(
      'Sommerfest',
    )
    expect(
      (page.get('.festival-start-field input').element as HTMLInputElement).value.length,
    ).toBeGreaterThan(0)
  })

  it('saves the new name as soon as the admin leaves the field', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-name-field input').exists()).toBe(true))
    await page.get('.festival-name-field input').setValue('Sommerfest 2027')
    await page.get('.festival-name-field input').trigger('blur')

    await vi.waitFor(() => {
      const sent = calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}`)
      expect((sent?.body as { name: string }).name).toBe('Sommerfest 2027')
    })
  })

  it('sends nothing when the admin leaves a field without changing anything', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-name-field input').exists()).toBe(true))
    await page.get('.festival-name-field input').trigger('blur')

    expect(writtenCalls(calls)).toEqual([])
  })

  it('asks for a name when the admin empties the field', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-name-field input').exists()).toBe(true))
    await page.get('.festival-name-field input').setValue('')
    await page.get('.festival-name-field input').trigger('blur')
    await page.vm.$nextTick()

    expect(page.get('.festival-name-field .v-messages__message').text()).toBe(
      'Geben Sie dem Fest einen Namen.',
    )
    expect(writtenCalls(calls)).toEqual([])
    expect((page.get('.festival-name-field input').element as HTMLInputElement).value).toBe('')
  })

  it('sends one change once when the admin presses enter and then leaves the field', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-name-field input').exists()).toBe(true))
    await page.get('.festival-name-field input').setValue('Sommerfest 2027')
    await page.get('.festival-name-field input').trigger('keyup.enter')
    await page.get('.festival-name-field input').trigger('blur')

    await vi.waitFor(() => expect(writtenCalls(calls).length).toBeGreaterThan(0))
    await new Promise((carryOn) => setTimeout(carryOn, 20))
    expect(writtenCalls(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
      `PUT /api/admin/festivals/${FESTIVAL_ID}`,
    ])
  })

  it('moves the admin to the overview when the laptop knows no such festival', async () => {
    stubLaptop({ festivals: [] })

    mountPage('fest-gone')

    await vi.waitFor(() => expect(window.location.pathname).toBe('/admin/overview'))
  })

  it('asks the laptop for this festival again when the connection comes back', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-name').exists()).toBe(true))
    calls.length = 0
    await useConnectionStore().refetchAll()

    expect(calls.map((call) => call.url)).toContain(
      `/api/admin/items?festivalId=${FESTIVAL_ID}`,
    )
  })
})

describe('the stations of this festival', () => {
  it('lists the ones that are at the festival and nothing else', async () => {
    stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-station-row').exists()).toBe(true))

    expect(page.findAll('.festival-station-row .name').map((row) => row.text())).toEqual(['Küche'])
  })

  it('keeps an empty row in place while none belongs to the festival', async () => {
    stubLaptop({ stations: [BAR] })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('.festival-station-placeholder').exists()).toBe(true),
    )

    expect(page.find('.festival-station-row').exists()).toBe(false)
    expect(page.find('.station-search').exists()).toBe(true)
  })

  it('offers only the stations that are switched on and not here yet', async () => {
    stubLaptop({
      stations: [KITCHEN, BAR, { ...BAR, stationId: 'station-off', name: 'Zelt', isActive: false }],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.station-search').exists()).toBe(true))

    expect(page.findAllComponents(VAutocomplete)[0].props('items')).toEqual([BAR])
  })

  it('waits for the button before it adds the station the admin picked', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.station-search').exists()).toBe(true))
    await page.findAllComponents(VAutocomplete)[0].setValue(BAR_ID)
    await page.vm.$nextTick()

    expect(writtenCalls(calls)).toEqual([])

    await page.get('.add-station').trigger('click')

    await vi.waitFor(() => {
      const sent = calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}/stations/${BAR_ID}`)
    })
  })

  it('creates a station in the popup and only offers it for adding afterwards', async () => {
    const calls = stubLaptop({ created: { stationId: 'station-neu' } })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.new-station').exists()).toBe(true))
    await page.get('.new-station').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.station-form')).not.toBeNull())
    writeInto('.station-form .station-name-field input', 'Zelt')
    await vi.waitFor(() =>
      expect((inDialog('.station-form button') as HTMLButtonElement).disabled).toBe(false),
    )
    inDialog('.station-form button').click()

    await vi.waitFor(() =>
      expect(document.querySelector('.new-station-dialog')).toBeNull(),
    )
    expect(writtenCalls(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
      'POST /api/admin/stations',
    ])
    expect(page.findAllComponents(VAutocomplete)[0].props('modelValue')).toBe('station-neu')
  })

  it('asks before a station leaves the festival', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.remove-station').exists()).toBe(true))
    await page.get('.remove-station').trigger('click')
    await waitForDialog()

    expect(calls.some((call) => call.method === 'DELETE')).toBe(false)
  })

  it('lets it leave once the question is answered with yes', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.remove-station').exists()).toBe(true))
    await page.get('.remove-station').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(calls.find((call) => call.method === 'DELETE')?.url).toBe(
        `/api/admin/festivals/${FESTIVAL_ID}/stations/${KITCHEN_ID}`,
      ),
    )
  })

  it('tints every second station row so the eye can follow it', async () => {
    stubLaptop({ stations: [KITCHEN, { ...BAR, isAtTheFestival: true }] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.findAll('.festival-station-row').length).toBe(2))

    const rows = page.findAll('.festival-station-row')
    expect(rows[0].classes()).not.toContain('tinted-row')
    expect(rows[1].classes()).toContain('tinted-row')
  })

  it('names how many orders the station already took when the laptop keeps it', async () => {
    stubLaptop({
      refusal: {
        method: 'DELETE',
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'admin.stationHasOrdersAtTheFestival',
          parameters: { count: '3' },
          details: null,
        },
      },
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.remove-station').exists()).toBe(true))
    await page.get('.remove-station').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(page.get('.festival-station-row .refusal').text()).toBe(
        'Ausgabestellen mit aufgegebenen Bestellungen können nicht entfernt werden.',
      ),
    )
  })
})

describe('the items of this festival', () => {
  it('lists them under their category with their price and their stations', async () => {
    stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-item-row').exists()).toBe(true))

    expect(page.get('.category-name').text()).toBe('Speisen')
    expect(page.findAll('.festival-item-row .name').map((row) => row.text())).toEqual(['Bratwurst'])
    expect((page.get('.price-field input').element as HTMLInputElement).value).toBe('3,50')
    expect(page.get('.festival-item-row .station-select').text()).toBe('Küche')
  })

  it('asks for a station first while the festival has none', async () => {
    stubLaptop({ stations: [BAR] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-item-placeholder').exists()).toBe(true))

    expect(page.get('.needs-a-station').text()).toBe(
      'Dieses Fest hat noch keine Ausgabestelle. Fügen Sie zuerst eine hinzu.',
    )
    expect(page.find('.item-search').exists()).toBe(false)
    expect(page.find('.add-item').exists()).toBe(false)
  })

  it('opens the placement dialog for the item the admin chose and clears the search', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.item-search').exists()).toBe(true))
    await openPlacementDialog(page)

    expect(document.querySelector('.placement-title')?.textContent).toContain('Bier')
    expect(page.findAll('.festival-item-row').length).toBe(1)
    expect(writtenCalls(calls)).toEqual([])
    expect(page.findAllComponents(VAutocomplete)[1].props('modelValue')).toBeNull()
  })

  it('refuses a missing price and a missing station in the dialog, naming the item', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.item-search').exists()).toBe(true))
    await openPlacementDialog(page)

    inDialog('.placement-dialog .place-item').click()
    await page.vm.$nextTick()

    expect(
      document.querySelector('.placement-dialog .price-field .v-messages__message')?.textContent,
    ).toBe('Tragen Sie den Preis in Euro ein, zum Beispiel 3,50.')
    expect(
      document.querySelector('.placement-dialog .station-select-error')?.textContent?.trim(),
    ).toBe('Bier braucht mindestens eine Ausgabestelle.')
    expect(writtenCalls(calls)).toEqual([])
  })

  it('places the item through the dialog and lists the row afterwards', async () => {
    const listed = listedItems()
    const calls = stubLaptop({
      items: listed,
      waitBeforeAnswering: async (call) => {
        if (call.method === 'PUT' && call.url.endsWith(`/items/${BEER_ID}`)) {
          const body = call.body as { priceCents: number; stationIds: string[] }
          const beer = listed.find((item) => item.itemId === BEER_ID)
          if (beer !== undefined) {
            beer.atTheFestival = {
              priceCents: body.priceCents,
              isAvailable: true,
              stationIds: body.stationIds,
            }
          }
        }
      },
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.item-search').exists()).toBe(true))
    await openPlacementDialog(page)

    writeInto('.placement-dialog .price-field input', '4,20')
    const boxes = await openDialogStations(page)
    await boxes[0].setValue(true)
    inDialog('.placement-dialog .place-item').click()

    await vi.waitFor(() => {
      const sent = writtenCalls(calls).find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}/items/${BEER_ID}`)
      expect(sent?.body).toEqual({ priceCents: 420, stationIds: [KITCHEN_ID] })
    })
    await vi.waitFor(() => expect(document.querySelector('.placement-dialog')).toBeNull())
    await vi.waitFor(() => expect(page.findAll('.festival-item-row').length).toBe(2))
    expect(page.findAll('.festival-item-row .name').map((row) => row.text())).toEqual([
      'Bier',
      'Bratwurst',
    ])
    expect(page.findAll('.festival-item-row .station-select')[0].text()).toBe('Küche')
  })

  it('summarises the stations on the row and refuses to empty the selection', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('.festival-item-row .station-select').exists()).toBe(true),
    )
    expect(page.get('.festival-item-row .station-select').text()).toBe('Küche')

    const boxes = await openRowStations(page)
    await boxes[0].setValue(false)

    await vi.waitFor(() =>
      expect(page.get('.festival-item-row .refusal').text()).toBe(
        'Bratwurst braucht mindestens eine Ausgabestelle.',
      ),
    )
    expect(boxes[0].props('modelValue')).toBe(true)
    expect(page.get('.festival-item-row .station-select').text()).toBe('Küche')
    expect(writtenCalls(calls)).toEqual([])
  })

  it('sends the price with a newly chosen station and summarises both stations', async () => {
    const listed = listedItems()
    const calls = stubLaptop({
      stations: [KITCHEN, { ...BAR, isAtTheFestival: true }],
      items: listed,
      waitBeforeAnswering: async (call) => {
        if (call.method === 'PUT' && call.url.endsWith(`/items/${SAUSAGE_ID}`)) {
          const body = call.body as { priceCents: number; stationIds: string[] }
          const sausage = listed.find((item) => item.itemId === SAUSAGE_ID)
          if (sausage !== undefined && sausage.atTheFestival !== null) {
            sausage.atTheFestival.stationIds = body.stationIds
          }
        }
      },
    })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('.festival-item-row .station-select').exists()).toBe(true),
    )

    const boxes = await openRowStations(page)
    await boxes[1].setValue(true)

    await vi.waitFor(() => {
      const sent = calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}`)
      expect(sent?.body).toEqual({ priceCents: 350, stationIds: [KITCHEN_ID, BAR_ID] })
    })
    await vi.waitFor(() =>
      expect(page.get('.festival-item-row .station-select').text()).toBe('Küche, Theke'),
    )
  })

  it('sends the new price with the stations the row already carries', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-item-row').exists()).toBe(true))
    await page.get('.price-field input').setValue('4,00')
    await page.get('.price-field input').trigger('blur')

    await vi.waitFor(() => {
      const sent = calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}`)
      expect(sent?.body).toEqual({ priceCents: 400, stationIds: [KITCHEN_ID] })
    })
  })

  it('tints every second item row so the eye can follow it', async () => {
    stubLaptop({
      items: [
        SAUSAGE,
        { ...BEER, atTheFestival: { priceCents: 400, isAvailable: true, stationIds: [KITCHEN_ID] } },
      ],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.findAll('.festival-item-row').length).toBe(2))

    const rows = page.findAll('.festival-item-row')
    expect(rows[0].classes()).not.toContain('tinted-row')
    expect(rows[1].classes()).toContain('tinted-row')
  })

  it('says once that the price cannot be read, at the field the admin typed in', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-item-row').exists()).toBe(true))
    await page.get('.price-field input').setValue('drei euro')
    await page.get('.price-field input').trigger('blur')
    await page.vm.$nextTick()

    expect(page.get('.price-field .v-messages__message').text()).toBe(
      'Tragen Sie den Preis in Euro ein, zum Beispiel 3,50.',
    )
    expect(page.find('.festival-item-row .refusal').exists()).toBe(false)
    expect(writtenCalls(calls)).toEqual([])
  })

  it('keeps the stations as they were while the price in that row cannot be read', async () => {
    const calls = stubLaptop({ stations: [KITCHEN, { ...BAR, isAtTheFestival: true }] })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('.festival-item-row .station-select').exists()).toBe(true),
    )
    await page.get('.price-field input').setValue('drei euro')
    const boxes = await openRowStations(page)
    await boxes[1].setValue(true)
    await page.vm.$nextTick()

    expect(boxes[1].props('modelValue')).toBe(false)
    expect(writtenCalls(calls)).toEqual([])
    expect(page.get('.festival-item-row .refusal').text()).toBe(
      'Tragen Sie einen Preis zwischen 0,00 und 999,99 Euro ein.',
    )
  })

  it('counts the tint through the whole list instead of starting over at each category', async () => {
    stubLaptop({
      categories: [FOOD, DRINKS],
      items: [
        SAUSAGE,
        {
          ...BEER,
          categoryId: DRINKS_ID,
          atTheFestival: { priceCents: 400, isAvailable: true, stationIds: [KITCHEN_ID] },
        },
      ],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.findAll('.festival-item-row').length).toBe(2))

    const rows = page.findAll('.festival-item-row')
    expect(rows[0].classes()).not.toContain('tinted-row')
    expect(rows[1].classes()).toContain('tinted-row')
  })

  it('marks an item sold out at this festival alone', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.sold-out-switch').exists()).toBe(true))
    expect(page.findComponent({ name: 'VSwitch' }).props('color')).toBe('primary')
    await page.get('.sold-out-switch input').setValue(true)

    await vi.waitFor(() =>
      expect(calls.map((call) => call.url)).toContain(
        `/api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}/availability`,
      ),
    )
  })

  it('asks before an item leaves this festival', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.remove-item').exists()).toBe(true))
    await page.get('.remove-item').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(calls.find((call) => call.method === 'DELETE')?.url).toBe(
        `/api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}`,
      ),
    )
  })

  it('offers only the items that are switched on and not here yet', async () => {
    stubLaptop({
      items: [SAUSAGE, BEER, { ...BEER, itemId: 'item-off', name: 'Wasser', isActive: false }],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.item-search').exists()).toBe(true))

    expect(
      (page.findAllComponents(VAutocomplete)[1].props('items') as { name: string }[]).map(
        (item) => item.name,
      ),
    ).toEqual(['Bier'])
  })

  it('creates an item in the popup and opens the placement dialog for it', async () => {
    const calls = stubLaptop({
      created: { itemId: 'item-neu' },
      items: [
        SAUSAGE,
        BEER,
        {
          itemId: 'item-neu',
          name: 'Pommes',
          categoryId: FOOD_ID,
          sortOrder: 3,
          isActive: true,
          productionMinutes: null,
          atTheFestival: null,
        },
      ],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.new-item').exists()).toBe(true))
    await page.get('.new-item').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.new-item-dialog')).not.toBeNull())
    await page.findComponent({ name: 'ItemForm' }).vm.$emit('save', {
      name: 'Pommes',
      categoryId: FOOD_ID,
      sortOrder: 1,
      productionMinutes: null,
    })

    await vi.waitFor(() => expect(document.querySelector('.new-item-dialog')).toBeNull())
    await vi.waitFor(() => expect(document.querySelector('.placement-dialog')).not.toBeNull())
    expect(document.querySelector('.placement-title')?.textContent).toContain('Pommes')
    expect(writtenCalls(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
      'POST /api/admin/items',
    ])
  })

  it('will not let the admin cancel a placement while the laptop is still answering', async () => {
    let releaseTheAnswer = (): void => {}
    const theAnswer = new Promise<void>((carryOn) => {
      releaseTheAnswer = carryOn
    })
    stubLaptop({
      waitBeforeAnswering: async (call) => {
        if (call.method === 'PUT') {
          await theAnswer
        }
      },
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.item-search').exists()).toBe(true))
    await openPlacementDialog(page)
    writeInto('.placement-dialog .price-field input', '4,20')
    const boxes = await openDialogStations(page)
    await boxes[0].setValue(true)
    inDialog('.placement-dialog .place-item').click()

    await vi.waitFor(() =>
      expect((inDialog('.placement-dialog .cancel') as HTMLButtonElement).disabled).toBe(true),
    )

    releaseTheAnswer()
    await vi.waitFor(() => expect(document.querySelector('.placement-dialog')).toBeNull())
  })

  it('says the items could not be loaded when the laptop cannot answer', async () => {
    stubLaptop({
      refuses: (call) =>
        call.method === 'GET' && call.url.startsWith('/api/admin/items')
          ? { status: 500, body: null }
          : null,
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-items .error').exists()).toBe(true))

    expect(page.get('.festival-items .error').text()).toBe(
      'Laden Sie die Seite neu. Die Daten konnten nicht geladen werden.',
    )
  })
})

describe('an item change the laptop refuses', () => {
  const REFUSED_PUT = {
    method: 'PUT',
    status: 400,
    body: {
      code: 'Conflict',
      messageKey: 'admin.actionFailed',
      parameters: {},
      details: null,
    },
  }

  const REFUSAL_TEXT =
    'Das hat nicht geklappt. Versuchen Sie es noch einmal, und laden Sie die Seite neu, wenn es wieder nicht klappt.'

  it('puts the station selection back where the laptop has it and says why', async () => {
    stubLaptop({
      stations: [KITCHEN, { ...BAR, isAtTheFestival: true }],
      refusal: REFUSED_PUT,
    })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('.festival-item-row .station-select').exists()).toBe(true),
    )
    const boxes = await openRowStations(page)
    await boxes[1].setValue(true)

    await vi.waitFor(() =>
      expect(page.get('.festival-item-row .refusal').text()).toBe(REFUSAL_TEXT),
    )
    await page.vm.$nextTick()
    expect(boxes[0].props('modelValue')).toBe(true)
    expect(boxes[1].props('modelValue')).toBe(false)
  })

  it('puts the price back to the one the laptop has', async () => {
    stubLaptop({ refusal: REFUSED_PUT })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-item-row').exists()).toBe(true))
    await page.get('.price-field input').setValue('4,00')
    await page.get('.price-field input').trigger('blur')

    await vi.waitFor(() =>
      expect(page.get('.festival-item-row .refusal').text()).toBe(REFUSAL_TEXT),
    )
    await page.vm.$nextTick()
    expect((page.get('.price-field input').element as HTMLInputElement).value).toBe('3,50')
  })

  it('leaves the sold out switch as the laptop has it', async () => {
    stubLaptop({
      refusal: {
        method: 'POST',
        status: 400,
        body: {
          code: 'Conflict',
          messageKey: 'admin.actionFailed',
          parameters: {},
          details: null,
        },
      },
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.sold-out-switch').exists()).toBe(true))
    await page.get('.sold-out-switch input').setValue(true)

    await vi.waitFor(() =>
      expect(page.get('.festival-item-row .refusal').text()).toBe(REFUSAL_TEXT),
    )
    await page.vm.$nextTick()
    expect((page.get('.sold-out-switch input').element as HTMLInputElement).checked).toBe(false)
  })

  it('sends a second change to the same row only once the first one is answered', async () => {
    let releaseTheFirstAnswer = (): void => {}
    const theFirstAnswer = new Promise<void>((carryOn) => {
      releaseTheFirstAnswer = carryOn
    })
    let held = false
    const calls = stubLaptop({
      stations: [KITCHEN, { ...BAR, isAtTheFestival: true }],
      waitBeforeAnswering: async (call) => {
        if (call.method === 'PUT' && !held) {
          held = true
          await theFirstAnswer
        }
      },
    })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('.festival-item-row .station-select').exists()).toBe(true),
    )
    const boxes = await openRowStations(page)
    await boxes[1].setValue(true)
    await boxes[1].setValue(false)

    expect(writtenCalls(calls).length).toBe(1)

    releaseTheFirstAnswer()
    await vi.waitFor(() => expect(writtenCalls(calls).length).toBe(2))
    await new Promise((carryOn) => setTimeout(carryOn, 20))

    expect(writtenCalls(calls).map((call) => call.body)).toEqual([
      { priceCents: 350, stationIds: [KITCHEN_ID, BAR_ID] },
      { priceCents: 350, stationIds: [KITCHEN_ID] },
    ])
    await vi.waitFor(() => expect(boxes[1].props('modelValue')).toBe(false))
  })

  it('sends one price change once when the admin presses enter and then leaves the field', async () => {
    const calls = stubLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.festival-item-row').exists()).toBe(true))
    await page.get('.price-field input').setValue('4,00')
    await page.get('.price-field input').trigger('keyup.enter')
    await page.get('.price-field input').trigger('blur')

    await vi.waitFor(() => expect(writtenCalls(calls).length).toBeGreaterThan(0))
    await new Promise((carryOn) => setTimeout(carryOn, 20))
    expect(writtenCalls(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
      `PUT /api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}`,
    ])
  })

  it('shows the price the laptop holds after another tab changed it', async () => {
    const listed = [SAUSAGE, BEER]
    stubLaptop({ items: listed })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.price-field input').exists()).toBe(true))
    expect((page.get('.price-field input').element as HTMLInputElement).value).toBe('3,50')

    listed[0] = {
      ...SAUSAGE,
      atTheFestival: { priceCents: 500, isAvailable: true, stationIds: [KITCHEN_ID] },
    }
    await useConnectionStore().refetchAll()

    await vi.waitFor(() =>
      expect((page.get('.price-field input').element as HTMLInputElement).value).toBe('5,00'),
    )
  })

  it('keeps a refusal on the row whose save the laptop refused while a later row saves at the same time', async () => {
    let releaseTheRefusedAnswer = (): void => {}
    const theRefusedAnswer = new Promise<void>((carryOn) => {
      releaseTheRefusedAnswer = carryOn
    })
    const listed: AdminItem[] = [
      {
        ...SAUSAGE,
        atTheFestival: {
          ...SAUSAGE.atTheFestival,
          stationIds: [...SAUSAGE.atTheFestival.stationIds],
        },
      },
      { ...BEER, atTheFestival: { priceCents: 400, isAvailable: true, stationIds: [KITCHEN_ID] } },
    ]
    const calls = stubLaptop({
      items: listed,
      refuses: (call) =>
        call.method === 'PUT' && call.url.endsWith(`/items/${BEER_ID}`)
          ? { status: REFUSED_PUT.status, body: REFUSED_PUT.body }
          : null,
      waitBeforeAnswering: async (call) => {
        if (call.method === 'PUT' && call.url.endsWith(`/items/${BEER_ID}`)) {
          await theRefusedAnswer
        }
        if (call.method === 'PUT' && call.url.endsWith(`/items/${SAUSAGE_ID}`)) {
          const body = call.body as { priceCents: number }
          listed[0].atTheFestival = {
            priceCents: body.priceCents,
            isAvailable: true,
            stationIds: [KITCHEN_ID],
          }
        }
      },
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.findAll('.price-field input').length).toBe(2))

    await page.findAll('.price-field input')[0].setValue('4,50')
    await page.findAll('.price-field input')[0].trigger('blur')
    await page.findAll('.price-field input')[1].setValue('5,00')
    await page.findAll('.price-field input')[1].trigger('blur')
    await vi.waitFor(() => expect(writtenCalls(calls).length).toBe(2))

    releaseTheRefusedAnswer()
    await vi.waitFor(() => expect(page.findAll('.festival-item-row .refusal').length).toBe(1))

    const rows = page.findAll('.festival-item-row')
    expect(rows[0].get('.refusal').text()).toBe(REFUSAL_TEXT)
    expect(rows[1].find('.refusal').exists()).toBe(false)
    expect((page.findAll('.price-field input')[0].element as HTMLInputElement).value).toBe('4,00')
    expect((page.findAll('.price-field input')[1].element as HTMLInputElement).value).toBe('5,00')
  })

  it('keeps the placement dialog open and says why the laptop refused', async () => {
    stubLaptop({ refusal: REFUSED_PUT })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('.item-search').exists()).toBe(true))
    await openPlacementDialog(page)
    writeInto('.placement-dialog .price-field input', '4,20')
    const boxes = await openDialogStations(page)
    await boxes[0].setValue(true)
    inDialog('.placement-dialog .place-item').click()

    await vi.waitFor(() =>
      expect(inDialog('.placement-dialog .refusal').textContent?.trim()).toBe(REFUSAL_TEXT),
    )
    expect(document.querySelector('.placement-dialog')).not.toBeNull()
  })
})
