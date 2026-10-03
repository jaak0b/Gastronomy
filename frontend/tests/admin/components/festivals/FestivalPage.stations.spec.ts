import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { VAutocomplete } from 'vuetify/components'
import { pressInDialog, waitForDialog, onScreen, typeInto } from '../../../support/dom'
import { FESTIVAL_ID, KITCHEN_ID, BAR_ID, KITCHEN, BAR, mountPage, stationRow, festivalLaptop } from './festivalPageFixture'
import { nextTick } from 'vue'

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the stations of this festival', () => {
  it('lists the ones that are at the festival and nothing else', async () => {
    festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-station-row"]').exists()).toBe(true))

    expect(page.findAll('[data-test="station-name"]').map((row) => row.text())).toEqual(['Küche'])
  })

  it('keeps an empty row in place while none belongs to the festival', async () => {
    festivalLaptop({ stations: [BAR] })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.find('[data-test="festival-station-placeholder"]').exists()).toBe(true),
    )

    expect(page.find('[data-test="festival-station-row"]').exists()).toBe(false)
    expect(page.find('[data-test="station-search"]').exists()).toBe(true)
  })

  it('offers only the stations that are switched on and not here yet', async () => {
    festivalLaptop({
      stations: [KITCHEN, BAR, { ...BAR, stationId: 'station-off', name: 'Zelt', isActive: false }],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="station-search"]').exists()).toBe(true))

    expect(page.getComponent<typeof VAutocomplete>('[data-test="station-search"]').props('items')).toEqual([BAR])
  })

  it('waits for the button before it adds the station the admin picked', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="station-search"]').exists()).toBe(true))
    await page.getComponent<typeof VAutocomplete>('[data-test="station-search"]').setValue(BAR_ID)
    await nextTick()

    expect(laptop.writes()).toEqual([])

    await page.get('[data-test="add-station"]').trigger('click')

    await vi.waitFor(() => {
      const sent = laptop.calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}/stations/${BAR_ID}`)
    })
  })

  it('creates a station in the popup and only offers it for adding afterwards', async () => {
    const NEW_STATION = {
      stationId: 'station-neu',
      name: 'Zelt',
      sortOrder: 3,
      isActive: true,
      hasDevice: false,
      isAtAnyFestival: false,
    }
    const laptop = festivalLaptop({ created: NEW_STATION })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="new-station"]').exists()).toBe(true))
    await page.get('[data-test="new-station"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="station-name-field"]')).not.toBeNull())
    typeInto('[data-test="station-name-field"] input', 'Zelt')
    await vi.waitFor(() =>
      expect((onScreen('[data-test="form-save"]') as HTMLButtonElement).disabled).toBe(false),
    )
    onScreen('[data-test="form-save"]').click()

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"]')).toBeNull(),
    )
    expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
      'POST /api/admin/stations',
    ])
    expect(page.getComponent<typeof VAutocomplete>('[data-test="station-search"]').props('modelValue')).toBe('station-neu')
    expect(page.getComponent<typeof VAutocomplete>('[data-test="station-search"]').props('items')).toEqual([BAR, NEW_STATION])
  })

  it('renames the station through the dialog on its row', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="edit-station"]').exists()).toBe(true))
    await page.get('[data-test="edit-station"]').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"] [data-test="station-name-field"]')).not.toBeNull(),
    )
    typeInto('[data-test="form-dialog"] [data-test="station-name-field"] input', 'Küche Nord')
    onScreen('[data-test="form-dialog"] [data-test="form-save"]').click()

    await vi.waitFor(() => {
      const sent = laptop.calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/stations/${KITCHEN_ID}`)
      expect(sent?.body).toEqual({ name: 'Küche Nord', sortOrder: 1 })
    })
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
  })

  it('asks before a station leaves the festival', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="remove-station"]').exists()).toBe(true))
    await page.get('[data-test="remove-station"]').trigger('click')
    await waitForDialog()

    expect(laptop.calls.some((call) => call.method === 'DELETE')).toBe(false)
  })

  it('lets it leave once the question is answered with yes', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="remove-station"]').exists()).toBe(true))
    await page.get('[data-test="remove-station"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.calls.find((call) => call.method === 'DELETE')?.url).toBe(
        `/api/admin/festivals/${FESTIVAL_ID}/stations/${KITCHEN_ID}`,
      ),
    )
  })

  it('tints every second station row so the eye can follow it', async () => {
    festivalLaptop({ stations: [KITCHEN, { ...BAR, isAtAnyFestival: true }] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.findAll('[data-test="festival-station-row"]').length).toBe(2))

    expect(stationRow(page, KITCHEN_ID).classes()).not.toContain('tinted-row')
    expect(stationRow(page, BAR_ID).classes()).toContain('tinted-row')
  })

  it('names how many orders the station already took when the laptop keeps it', async () => {
    festivalLaptop({
      refusal: {
        method: 'DELETE',
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'errors.admin.festivals.stationHasOrdersAtTheFestival',
          parameters: { count: '3' },
          details: null,
        },
      },
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="remove-station"]').exists()).toBe(true))
    await page.get('[data-test="remove-station"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(stationRow(page, KITCHEN_ID).get('[data-test="refusal"]').text()).toBe(
        'Solange das Fest aktiv ist, kann eine Ausgabestelle mit offenen Bestellungen nicht entfernt werden.',
      ),
    )
  })
})
