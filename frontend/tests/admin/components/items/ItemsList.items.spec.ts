import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { pressInDialog, waitForDialog } from '../../../support/dom'
import { ITEM_ID, ONE_FESTIVAL, ONE_ITEM, DEACTIVATED_ITEM, mountList, itemsListLaptop } from './itemsListFixture'
import { nextTick } from 'vue'
import { cancelTheFormDialog, formDialogHolding } from '../../../support/formDialogs'

describe('deactivating an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks before it happens', async () => {
    const laptop = itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))
    await list.get('[data-test="deactivate-item"]').trigger('click')

    await waitForDialog()

    expect(laptop.urls().some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('deactivates it once the question is answered with yes', async () => {
    const laptop = itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))
    await list.get('[data-test="deactivate-item"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() => expect(laptop.urls()).toContain(`/api/admin/items/${ITEM_ID}/deactivate`))
  })

  it('keeps the global deactivate disabled while the item is on the running festival', async () => {
    const laptop = itemsListLaptop({
      festivals: { festivals: [{ ...ONE_FESTIVAL.festivals[0], isRunning: true }] },
      items: {
        items: [
          { ...ONE_ITEM.items[0] },
          {
            ...ONE_ITEM.items[0],
            itemId: 'aaaa1111-2222-4333-8444-555566667777',
            name: 'Wasser',
            atTheFestival: null,
          },
        ],
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('[data-test="deactivate-item"]').length).toBe(2))

    const onTheMenuRow = list.get(`[data-test="item-row"][data-test-id="${ITEM_ID}"]`)
    const elsewhereRow = list.get('[data-test="item-row"][data-test-id="aaaa1111-2222-4333-8444-555566667777"]')
    const onTheMenu = onTheMenuRow.get('[data-test="deactivate-item"]').element as HTMLButtonElement
    const elsewhere = elsewhereRow.get('[data-test="deactivate-item"]').element as HTMLButtonElement
    expect(onTheMenu.disabled).toBe(true)
    expect(elsewhere.disabled).toBe(false)

    const wrapper = onTheMenuRow.get('[data-test="deactivate-wrapper"]')
    const tooltip = wrapper.findComponent({ name: 'VTooltip' })
    expect(tooltip.exists()).toBe(true)
    expect(tooltip.props('disabled')).toBe(false)

    await wrapper.trigger('mouseenter')
    await vi.waitFor(() => expect(document.querySelector('[data-test="festival-menu-tooltip"]')).not.toBeNull())
    expect(document.querySelector('[data-test="festival-menu-tooltip"]')?.textContent).toContain(
      'Ein Artikel auf der Karte eines aktiven Festes kann nicht abgeschaltet werden.',
    )

    onTheMenu.click()
    await nextTick()

    expect(document.querySelector('[data-test="confirm-dialog"]')).toBeNull()
    expect(laptop.urls().some((url) => url.endsWith('/deactivate'))).toBe(false)
  })
})

describe('an item that is deactivated', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is left out of the list until the admin asks to see deactivated entries', async () => {
    itemsListLaptop({ items: DEACTIVATED_ITEM })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="show-deactivated"]').exists()).toBe(true))

    expect(list.find('[data-test="item-row"]').exists()).toBe(false)
  })

  it('offers to activate it again without asking a question first', async () => {
    const laptop = itemsListLaptop({ items: DEACTIVATED_ITEM })

    const list = mountList()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))
    await list.get('[data-test="reactivate-item"]').trigger('click')

    await vi.waitFor(() => expect(laptop.urls()).toContain(`/api/admin/items/${ITEM_ID}/activate`))
  })
})

describe('adding an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks for the item in a dialog instead of on the page', async () => {
    itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="new-item"]').exists()).toBe(true))

    expect(document.querySelector('[data-test="form-dialog"]')).toBeNull()

    await list.get('[data-test="new-item"]').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
  })

  it('drops the refusal to switch a category off once the admin writes a new item', async () => {
    itemsListLaptop({
      refusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'errors.admin.categories.hasActiveItems',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))
    await list.get('[data-test="deactivate-category"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')
    await vi.waitFor(() => expect(list.find('[data-test="category-refusal"]').exists()).toBe(true))

    await list.get('[data-test="new-item"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
    await cancelTheFormDialog(formDialogHolding('[data-test="item-name-field"]'))
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())

    expect(list.find('[data-test="category-refusal"]').exists()).toBe(false)
  })
})
