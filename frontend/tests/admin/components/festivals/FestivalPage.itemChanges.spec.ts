import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { VAutocomplete } from 'vuetify/components'
import { pressInDialog, onScreen, typeInto } from '../../../support/dom'
import { FESTIVAL_ID, KITCHEN_ID, SAUSAGE_ID, FOOD_ID, SUMMER, SAUSAGE, BEER, mountPage, openPlacementDialog, stationBox, openDialogStations, festivalLaptop } from './festivalPageFixture'
import { nextTick } from 'vue'
import { saveTheItemDialog } from '../../../support/formDialogs'

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the items of this festival', () => {
  it('marks an item sold out at this festival alone', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="sold-out-switch"]').exists()).toBe(true))
    expect(page.findComponent({ name: 'VSwitch' }).props('color')).toBe('primary')
    await page.get('[data-test="sold-out-switch"] input').setValue(true)

    await vi.waitFor(() =>
      expect(laptop.calls.map((call) => call.url)).toContain(
        `/api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}/availability`,
      ),
    )
  })

  it('asks before an item leaves a festival that is not running', async () => {
    const laptop = festivalLaptop({ festivals: [{ ...SUMMER, isRunning: false }] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="remove-item"]').exists()).toBe(true))
    await page.get('[data-test="remove-item"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.calls.find((call) => call.method === 'DELETE')?.url).toBe(
        `/api/admin/festivals/${FESTIVAL_ID}/items/${SAUSAGE_ID}`,
      ),
    )
  })

  it('keeps the item on the menu while the festival runs', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="remove-item"]').exists()).toBe(true))

    const remove = page.get('[data-test="remove-item"]').element as HTMLButtonElement
    expect(remove.disabled).toBe(true)
    remove.click()
    await nextTick()

    expect(document.querySelector('[data-test="confirm-dialog"]')).toBeNull()
    expect(laptop.calls.some((call) => call.method === 'DELETE')).toBe(false)
  })

  it('offers the reason on the button while the festival runs', async () => {
    festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="remove-item-wrapper"]').exists()).toBe(true))

    const wrapper = page.get('[data-test="remove-item-wrapper"]')
    expect(getComputedStyle(wrapper.element).pointerEvents).not.toBe('none')

    const tooltip = wrapper.findComponent({ name: 'VTooltip' })
    expect(tooltip.exists()).toBe(true)
    expect(tooltip.props('disabled')).toBe(false)

    await wrapper.trigger('mouseenter')
    await vi.waitFor(() => expect(document.querySelector('[data-test="remove-item-tooltip"]')).not.toBeNull())
    expect(document.querySelector('[data-test="remove-item-tooltip"]')?.textContent).toContain(
      'Ein Artikel kann von einem aktiven Fest nicht entfernt werden.',
    )
  })

  it('offers only the items that are switched on and not here yet', async () => {
    festivalLaptop({
      items: [SAUSAGE, BEER, { ...BEER, itemId: 'item-off', name: 'Wasser', isActive: false }],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="item-search"]').exists()).toBe(true))

    expect(
      (page.getComponent<typeof VAutocomplete>('[data-test="item-search"]').props('items') as { name: string }[]).map(
        (item) => item.name,
      ),
    ).toEqual(['Bier'])
  })

  it('renames the category through the dialog on its heading', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="edit-category"]').exists()).toBe(true))
    await page.get('[data-test="edit-category"]').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"] [data-test="category-name-field"]')).not.toBeNull(),
    )
    typeInto('[data-test="form-dialog"] [data-test="category-name-field"] input', 'Speisen neu')
    onScreen('[data-test="form-dialog"] [data-test="form-save"]').click()

    await vi.waitFor(() => {
      const sent = laptop.calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/categories/${FOOD_ID}`)
      expect(sent?.body).toEqual({ name: 'Speisen neu', colourHex: '#FFEB3B' })
    })
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
  })

  it('renames the item through the dialog on its row', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="edit-item"]').exists()).toBe(true))
    await page.get('[data-test="edit-item"]').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"] [data-test="item-name-field"]')).not.toBeNull(),
    )
    typeInto('[data-test="form-dialog"] [data-test="item-name-field"] input', 'Bratwurst XL')
    onScreen('[data-test="form-dialog"] [data-test="form-save"]').click()

    await vi.waitFor(() => {
      const sent = laptop.calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/items/${SAUSAGE_ID}`)
      expect(sent?.body).toEqual({
        name: 'Bratwurst XL',
        categoryId: FOOD_ID,
        sortOrder: 1,
        productionMinutes: null,
        isQueueIndependent: false,
      })
    })
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
  })

  it('creates an item in the popup and opens the placement dialog for it', async () => {
    const laptop = festivalLaptop({
      created: {
        itemId: 'item-neu',
        name: 'Pommes',
        categoryId: FOOD_ID,
        sortOrder: 3,
        isActive: true,
        productionMinutes: null,
        isQueueIndependent: false,
  ingredients: [],
        atTheFestival: null,
      },
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
          isQueueIndependent: false,
  ingredients: [],
          atTheFestival: null,
        },
      ],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="new-item"]').exists()).toBe(true))
    await page.get('[data-test="new-item"]').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"] [data-test="item-name-field"]')).not.toBeNull(),
    )
    await saveTheItemDialog(page.findComponent({ name: 'ItemDialog' }), 'Pommes', FOOD_ID)

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"] [data-test="item-name-field"]')).toBeNull())
    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"] [data-test="price-field"]')).not.toBeNull(),
    )
    expect(document.querySelector('[data-test="form-dialog-title"]')?.textContent).toContain('Pommes')
    expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
      'POST /api/admin/items',
    ])
  })

  it('opens the placement dialog for the created item even when the list is still the old one', async () => {
    festivalLaptop({
      created: {
        itemId: 'item-neu',
        name: 'Pommes',
        categoryId: FOOD_ID,
        sortOrder: 3,
        isActive: true,
        productionMinutes: null,
        isQueueIndependent: false,
  ingredients: [],
        atTheFestival: null,
      },
      items: [SAUSAGE, BEER],
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="new-item"]').exists()).toBe(true))
    await page.get('[data-test="new-item"]').trigger('click')
    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"] [data-test="item-name-field"]')).not.toBeNull(),
    )
    await saveTheItemDialog(page.findComponent({ name: 'ItemDialog' }), 'Pommes', FOOD_ID)

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"] [data-test="price-field"]')).not.toBeNull(),
    )
    expect(document.querySelector('[data-test="form-dialog-title"]')?.textContent).toContain('Pommes')
  })

  it('will not let the admin cancel a placement while the laptop is still answering', async () => {
    let releaseTheAnswer = (): void => {}
    const theAnswer = new Promise<void>((carryOn) => {
      releaseTheAnswer = carryOn
    })
    festivalLaptop({
      waitBeforeAnswering: async (call) => {
        if (call.method === 'PUT') {
          await theAnswer
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

    await vi.waitFor(() =>
      expect((onScreen('[data-test="form-dialog"] [data-test="form-cancel"]') as HTMLButtonElement).disabled).toBe(true),
    )

    releaseTheAnswer()
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
  })

  it('says the items could not be loaded when the laptop cannot answer', async () => {
    festivalLaptop({
      refuses: (call) =>
        call.method === 'GET' && call.url.startsWith('/api/admin/items')
          ? { status: 500, body: null }
          : null,
    })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-items"] [data-test="load-failed"]').exists()).toBe(true))

    expect(page.get('[data-test="festival-items"] [data-test="load-failed"]').text()).toBe(
      'Laden Sie die Seite neu. Die Daten konnten nicht geladen werden.',
    )
  })
})
