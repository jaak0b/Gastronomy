import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { VAutocomplete } from 'vuetify/components'
import { FESTIVAL_ID, KITCHEN_ID, BAR_ID, SAUSAGE_ID, BEER_ID, KITCHEN, BAR, FOOD, SAUSAGE, DRINKS_ID, DRINKS, BEER, mountPage, openPlacementDialog, itemRow, stationBox, openRowStations, openDialogStations, listedItems, festivalLaptop } from './festivalPageFixture'
import { onScreen, typeInto } from '../../../support/dom'
import { nextTick } from 'vue'

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the items of this festival', () => {
  it('lists them under their category with their price and their stations', async () => {
    festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-item-row"]').exists()).toBe(true))

    expect(page.get('[data-test="category-name"]').text()).toBe('Speisen')
    expect(page.findAll('[data-test="item-name"]').map((row) => row.text())).toEqual(['Bratwurst'])
    expect((page.get('[data-test="price-field"] input').element as HTMLInputElement).value).toBe('3,50')
    expect(itemRow(page, SAUSAGE_ID).get('[data-test="station-select"]').text()).toBe('Küche')
  })

  it('asks for a station first while the festival has none', async () => {
    festivalLaptop({ stations: [BAR] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-item-placeholder"]').exists()).toBe(true))

    expect(page.get('[data-test="needs-a-station"]').text()).toBe(
      'Dieses Fest hat noch keine Ausgabestelle. Fügen Sie zuerst eine hinzu.',
    )
    expect(page.find('[data-test="item-search"]').exists()).toBe(false)
    expect(page.find('[data-test="add-item"]').exists()).toBe(false)
  })

  it('opens the placement dialog for the item the admin chose and clears the search', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="item-search"]').exists()).toBe(true))
    await openPlacementDialog(page)

    expect(document.querySelector('[data-test="form-dialog-title"]')?.textContent).toContain('Bier')
    expect(page.findAll('[data-test="festival-item-row"]').length).toBe(1)
    expect(laptop.writes()).toEqual([])
    expect(page.getComponent<typeof VAutocomplete>('[data-test="item-search"]').props('modelValue')).toBeNull()
  })

  it('refuses a missing price and a missing station in the dialog, naming the item', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="item-search"]').exists()).toBe(true))
    await openPlacementDialog(page)

    onScreen('[data-test="form-dialog"] [data-test="form-save"]').click()
    await nextTick()

    expect(
      document.querySelector('[data-test="form-dialog"] [data-test="price-field"] .v-messages__message')?.textContent,
    ).toBe('Tragen Sie den Preis in Euro ein, zum Beispiel 3,50.')
    expect(
      document.querySelector('[data-test="form-dialog"] [data-test="station-select-error"]')?.textContent?.trim(),
    ).toBe('Bier braucht mindestens eine Ausgabestelle.')
    expect(laptop.writes()).toEqual([])
  })

  it('places the item through the dialog and lists the row afterwards', async () => {
    const listed = listedItems()
    const laptop = festivalLaptop({
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
    await vi.waitFor(() => expect(page.find('[data-test="item-search"]').exists()).toBe(true))
    await openPlacementDialog(page)

    typeInto('[data-test="form-dialog"] [data-test="price-field"] input', '4,20')
    const stations = await openDialogStations(page)
    await stationBox(stations, KITCHEN_ID).setValue(true)
    onScreen('[data-test="form-dialog"] [data-test="form-save"]').click()

    await vi.waitFor(() => {
      const sent = laptop.writes().find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}/items/${BEER_ID}`)
      expect(sent?.body).toEqual({ priceCents: 420, stationIds: [KITCHEN_ID] })
    })
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
    await vi.waitFor(() => expect(page.findAll('[data-test="festival-item-row"]').length).toBe(2))
    expect(page.findAll('[data-test="item-name"]').map((row) => row.text())).toEqual([
      'Bier',
      'Bratwurst',
    ])
    expect(itemRow(page, BEER_ID).get('[data-test="station-select"]').text()).toBe('Küche')
  })

  it('summarises the stations on the row and refuses to empty the selection', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('[data-test="festival-item-row"] [data-test="station-select"]').exists()).toBe(true),
    )
    expect(itemRow(page, SAUSAGE_ID).get('[data-test="station-select"]').text()).toBe('Küche')

    const stations = await openRowStations(page)
    await stationBox(stations, KITCHEN_ID).setValue(false)

    await vi.waitFor(() =>
      expect(itemRow(page, SAUSAGE_ID).get('[data-test="refusal"]').text()).toBe(
        'Bratwurst braucht mindestens eine Ausgabestelle.',
      ),
    )
    expect(stationBox(stations, KITCHEN_ID).props('modelValue')).toBe(true)
    expect(itemRow(page, SAUSAGE_ID).get('[data-test="station-select"]').text()).toBe('Küche')
    expect(laptop.writes()).toEqual([])
  })

  it('sends the price with a newly chosen station and summarises both stations', async () => {
    const listed = listedItems()
    const laptop = festivalLaptop({
      stations: [KITCHEN, { ...BAR, isAtAnyFestival: true }],
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
      expect(page.find('[data-test="festival-item-row"] [data-test="station-select"]').exists()).toBe(true),
    )

    const stations = await openRowStations(page)
    await stationBox(stations, BAR_ID).setValue(true)

    await vi.waitFor(() => {
      const sent = laptop.calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}`)
      expect(sent?.body).toEqual({ priceCents: 350, stationIds: [KITCHEN_ID, BAR_ID] })
    })
    await vi.waitFor(() =>
      expect(itemRow(page, SAUSAGE_ID).get('[data-test="station-select"]').text()).toBe('Küche, Theke'),
    )
  })

  it('sends the new price with the stations the row already carries', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-item-row"]').exists()).toBe(true))
    await page.get('[data-test="price-field"] input').setValue('4,00')
    await page.get('[data-test="price-field"] input').trigger('blur')

    await vi.waitFor(() => {
      const sent = laptop.calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}`)
      expect(sent?.body).toEqual({ priceCents: 400, stationIds: [KITCHEN_ID] })
    })
  })

  it('tints every second item row so the eye can follow it', async () => {
    festivalLaptop({
      items: [
        SAUSAGE,
        { ...BEER, atTheFestival: { priceCents: 400, isAvailable: true, stationIds: [KITCHEN_ID] } },
      ],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.findAll('[data-test="festival-item-row"]').length).toBe(2))

    expect(itemRow(page, BEER_ID).classes()).not.toContain('tinted-row')
    expect(itemRow(page, SAUSAGE_ID).classes()).toContain('tinted-row')
  })

  it('says once that the price cannot be read, at the field the admin typed in', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-item-row"]').exists()).toBe(true))
    await page.get('[data-test="price-field"] input').setValue('drei euro')
    await page.get('[data-test="price-field"] input').trigger('blur')
    await nextTick()

    expect(page.get('[data-test="price-field"] .v-messages__message').text()).toBe(
      'Tragen Sie den Preis in Euro ein, zum Beispiel 3,50.',
    )
    expect(page.find('[data-test="festival-item-row"] [data-test="refusal"]').exists()).toBe(false)
    expect(laptop.writes()).toEqual([])
  })

  it('keeps the stations as they were while the price in that row cannot be read', async () => {
    const laptop = festivalLaptop({ stations: [KITCHEN, { ...BAR, isAtAnyFestival: true }] })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('[data-test="festival-item-row"] [data-test="station-select"]').exists()).toBe(true),
    )
    await page.get('[data-test="price-field"] input').setValue('drei euro')
    const stations = await openRowStations(page)
    await stationBox(stations, BAR_ID).setValue(true)
    await nextTick()

    expect(stationBox(stations, BAR_ID).props('modelValue')).toBe(false)
    expect(laptop.writes()).toEqual([])
    expect(itemRow(page, SAUSAGE_ID).get('[data-test="refusal"]').text()).toBe(
      'Tragen Sie einen Preis zwischen 0,00 und 999,99 Euro ein.',
    )
  })

  it('counts the tint through the whole list instead of starting over at each category', async () => {
    festivalLaptop({
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
    await vi.waitFor(() => expect(page.findAll('[data-test="festival-item-row"]').length).toBe(2))

    expect(itemRow(page, SAUSAGE_ID).classes()).not.toContain('tinted-row')
    expect(itemRow(page, BEER_ID).classes()).toContain('tinted-row')
  })
})
