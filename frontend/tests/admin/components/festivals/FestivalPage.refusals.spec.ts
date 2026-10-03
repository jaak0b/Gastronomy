import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { AdminItemView } from '../../../../src/shared/api/generatedSchemas'
import { useConnectionStore } from '../../../../src/shared/stores/connection'
import { FESTIVAL_ID, KITCHEN_ID, BAR_ID, SAUSAGE_ID, BEER_ID, KITCHEN, BAR, SAUSAGE, BEER, mountPage, openPlacementDialog, itemRow, stationBox, openRowStations, openDialogStations, festivalLaptop } from './festivalPageFixture'
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

describe('an item change the laptop refuses', () => {
  const REFUSED_PUT = {
    method: 'PUT',
    status: 400,
    body: {
      code: 'Conflict',
      messageKey: 'errors.admin.actionFailed',
      parameters: {},
      details: null,
    },
  }

  const REFUSAL_TEXT =
    'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.'

  it('puts the station selection back where the laptop has it and says why', async () => {
    festivalLaptop({
      stations: [KITCHEN, { ...BAR, isAtAnyFestival: true }],
      refusal: REFUSED_PUT,
    })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('[data-test="festival-item-row"] [data-test="station-select"]').exists()).toBe(true),
    )
    const stations = await openRowStations(page)
    await stationBox(stations, BAR_ID).setValue(true)

    await vi.waitFor(() =>
      expect(itemRow(page, SAUSAGE_ID).get('[data-test="refusal"]').text()).toBe(REFUSAL_TEXT),
    )
    await nextTick()
    expect(stationBox(stations, KITCHEN_ID).props('modelValue')).toBe(true)
    expect(stationBox(stations, BAR_ID).props('modelValue')).toBe(false)
  })

  it('puts the price back to the one the laptop has', async () => {
    festivalLaptop({ refusal: REFUSED_PUT })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-item-row"]').exists()).toBe(true))
    await page.get('[data-test="price-field"] input').setValue('4,00')
    await page.get('[data-test="price-field"] input').trigger('blur')

    await vi.waitFor(() =>
      expect(itemRow(page, SAUSAGE_ID).get('[data-test="refusal"]').text()).toBe(REFUSAL_TEXT),
    )
    await nextTick()
    expect((page.get('[data-test="price-field"] input').element as HTMLInputElement).value).toBe('3,50')
  })

  it('leaves the sold out switch as the laptop has it', async () => {
    festivalLaptop({
      refusal: {
        method: 'POST',
        status: 400,
        body: {
          code: 'Conflict',
          messageKey: 'errors.admin.actionFailed',
          parameters: {},
          details: null,
        },
      },
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="sold-out-switch"]').exists()).toBe(true))
    await page.get('[data-test="sold-out-switch"] input').setValue(true)

    await vi.waitFor(() =>
      expect(itemRow(page, SAUSAGE_ID).get('[data-test="refusal"]').text()).toBe(REFUSAL_TEXT),
    )
    await nextTick()
    expect((page.get('[data-test="sold-out-switch"] input').element as HTMLInputElement).checked).toBe(false)
  })

  it('sends a second change to the same row only once the first one is answered', async () => {
    let releaseTheFirstAnswer = (): void => {}
    const theFirstAnswer = new Promise<void>((carryOn) => {
      releaseTheFirstAnswer = carryOn
    })
    let held = false
    const laptop = festivalLaptop({
      stations: [KITCHEN, { ...BAR, isAtAnyFestival: true }],
      waitBeforeAnswering: async (call) => {
        if (call.method === 'PUT' && !held) {
          held = true
          await theFirstAnswer
        }
      },
    })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('[data-test="festival-item-row"] [data-test="station-select"]').exists()).toBe(true),
    )
    const stations = await openRowStations(page)
    await stationBox(stations, BAR_ID).setValue(true)
    await stationBox(stations, BAR_ID).setValue(false)

    expect(laptop.writes().length).toBe(1)

    releaseTheFirstAnswer()
    await vi.waitFor(() => expect(laptop.writes().length).toBe(2))
    await new Promise((carryOn) => setTimeout(carryOn, 20))

    expect(laptop.writes().map((call) => call.body)).toEqual([
      { priceCents: 350, stationIds: [KITCHEN_ID, BAR_ID] },
      { priceCents: 350, stationIds: [KITCHEN_ID] },
    ])
    await vi.waitFor(() => expect(stationBox(stations, BAR_ID).props('modelValue')).toBe(false))
  })

  it('sends one price change once when the admin presses enter and then leaves the field', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-item-row"]').exists()).toBe(true))
    await page.get('[data-test="price-field"] input').setValue('4,00')
    await page.get('[data-test="price-field"] input').trigger('keyup.enter')
    await page.get('[data-test="price-field"] input').trigger('blur')

    await vi.waitFor(() => expect(laptop.writes().length).toBeGreaterThan(0))
    await new Promise((carryOn) => setTimeout(carryOn, 20))
    expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
      `PUT /api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}`,
    ])
  })

  it('shows the price the laptop holds after another tab changed it', async () => {
    const listed = [SAUSAGE, BEER]
    festivalLaptop({ items: listed })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="price-field"] input').exists()).toBe(true))
    expect((page.get('[data-test="price-field"] input').element as HTMLInputElement).value).toBe('3,50')

    listed[0] = {
      ...SAUSAGE,
      atTheFestival: { priceCents: 500, isAvailable: true, stationIds: [KITCHEN_ID] },
    }
    await useConnectionStore().refetchAll()

    await vi.waitFor(() =>
      expect((page.get('[data-test="price-field"] input').element as HTMLInputElement).value).toBe('5,00'),
    )
  })

  it('keeps a refusal on the row whose save the laptop refused while a later row saves at the same time', async () => {
    let releaseTheRefusedAnswer = (): void => {}
    const theRefusedAnswer = new Promise<void>((carryOn) => {
      releaseTheRefusedAnswer = carryOn
    })
    const listed: AdminItemView[] = [
      {
        ...SAUSAGE,
        atTheFestival: {
          ...SAUSAGE.atTheFestival,
          stationIds: [...SAUSAGE.atTheFestival.stationIds],
        },
      },
      { ...BEER, atTheFestival: { priceCents: 400, isAvailable: true, stationIds: [KITCHEN_ID] } },
    ]
    const laptop = festivalLaptop({
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
    await vi.waitFor(() => expect(page.findAll('[data-test="festival-item-row"]').length).toBe(2))

    await itemRow(page, BEER_ID).get('[data-test="price-field"] input').setValue('4,50')
    await itemRow(page, BEER_ID).get('[data-test="price-field"] input').trigger('blur')
    await itemRow(page, SAUSAGE_ID).get('[data-test="price-field"] input').setValue('5,00')
    await itemRow(page, SAUSAGE_ID).get('[data-test="price-field"] input').trigger('blur')
    await vi.waitFor(() => expect(laptop.writes().length).toBe(2))

    releaseTheRefusedAnswer()
    await vi.waitFor(() => expect(page.findAll('[data-test="festival-item-row"] [data-test="refusal"]').length).toBe(1))

    expect(itemRow(page, BEER_ID).get('[data-test="refusal"]').text()).toBe(REFUSAL_TEXT)
    expect(itemRow(page, SAUSAGE_ID).find('[data-test="refusal"]').exists()).toBe(false)
    expect(
      (itemRow(page, BEER_ID).get('[data-test="price-field"] input').element as HTMLInputElement).value,
    ).toBe('4,00')
    expect(
      (itemRow(page, SAUSAGE_ID).get('[data-test="price-field"] input').element as HTMLInputElement).value,
    ).toBe('5,00')
  })

  it('keeps a refusal on its row when the laptop reloads the item list afterwards', async () => {
    festivalLaptop({ refusal: REFUSED_PUT })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="price-field"] input').exists()).toBe(true))
    await page.get('[data-test="price-field"] input').setValue('4,50')
    await page.get('[data-test="price-field"] input').trigger('blur')
    await vi.waitFor(() => expect(page.find('[data-test="festival-item-row"] [data-test="refusal"]').exists()).toBe(true))

    await useConnectionStore().refetchAll()
    await vi.waitFor(() => expect(page.find('[data-test="festival-item-row"] [data-test="refusal"]').exists()).toBe(true))

    expect(itemRow(page, SAUSAGE_ID).get('[data-test="refusal"]').text()).toBe(REFUSAL_TEXT)
  })

  it('keeps the placement dialog open and says why the laptop refused', async () => {
    festivalLaptop({ refusal: REFUSED_PUT })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="item-search"]').exists()).toBe(true))
    await openPlacementDialog(page)
    typeInto('[data-test="form-dialog"] [data-test="price-field"] input', '4,20')
    const stations = await openDialogStations(page)
    await stationBox(stations, KITCHEN_ID).setValue(true)
    onScreen('[data-test="form-dialog"] [data-test="form-save"]').click()

    await vi.waitFor(() =>
      expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(REFUSAL_TEXT),
    )
    expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull()
  })
})
