import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { stubLaptop, answer, refusal, noConnection } from '../../support/laptop'
import { nextTick } from 'vue'
import { CATALOG, mountCatalog, openCategory, itemRowNamed, tapToAdd, goBackToTheCategories } from './catalogFixture'

enableAutoUnmount(afterEach)

describe('building the order on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('puts one portion on the order when an item is tapped', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 'Essen')

    await tapToAdd(view, 'Bratwurst')
    await tapToAdd(view, 'Bratwurst')

    expect(order.draft.lines).toHaveLength(2)
    expect(view.get('[data-test="item-row"] [data-test="note-group"] [data-test="group-count"]').text()).toBe('2')
  })

  it('puts a portion carrying the typed note on a line of its own', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 'Essen')

    await tapToAdd(view, 'Bratwurst')
    await itemRowNamed(view, 'Bratwurst').get('[data-test="add-note"]').trigger('click')
    const field = document.querySelector('[data-test="note-dialog"] [data-test="note-input"] input') as HTMLInputElement
    field.value = 'ohne Eis'
    field.dispatchEvent(new Event('input'))
    await nextTick()
    ;(document.querySelector('[data-test="note-dialog"] [data-test="note-confirm"]') as HTMLElement).click()
    await nextTick()

    expect(order.draft.lines.map((line) => line.note)).toEqual([null, 'ohne Eis'])
    const groups = view.findAll('[data-test="item-row"] [data-test="note-group"]')
    expect(groups).toHaveLength(2)
    expect(
      groups
        .filter((group) => !group.find('[data-test="group-note"]').exists())
        .map((group) => group.get('[data-test="group-count"]').text()),
    ).toEqual(['1'])
    expect(
      groups
        .filter((group) => group.find('[data-test="group-note"]').exists())
        .map((group) => group.get('[data-test="group-note"]').text()),
    ).toEqual(['ohne Eis'])
  })

  it('takes the most recently added portion off again', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 'Essen')

    await tapToAdd(view, 'Bratwurst')
    await tapToAdd(view, 'Bratwurst')
    await view.get('[data-test="item-row"] [data-test="note-group"] [data-test="group-remove"]').trigger('click')

    expect(order.draft.lines).toHaveLength(1)
  })
})

describe('the way from the ordering screen to the summary', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
  })

  async function catalogWithOnePortion() {
    const view = mountCatalog()
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')
    await goBackToTheCategories(view)
    return view
  }

  it('keeps the way to the summary open while the table is empty, so tapping it can say so', async () => {
    const view = await catalogWithOnePortion()

    expect(view.get('[data-test="to-review"]').attributes('disabled')).toBeUndefined()
  })

  it('marks the table field instead of moving on when no table was entered', async () => {
    const view = await catalogWithOnePortion()

    await view.get('[data-test="to-review"]').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
    expect(view.get('[data-test="table-field"]').classes()).toContain('is-missing')
  })

  it('puts the cursor in the table field so the keyboard opens on the thing that is missing', async () => {
    const view = await catalogWithOnePortion()

    await view.get('[data-test="to-review"]').trigger('click')

    expect(document.activeElement).toBe(view.get('[data-test="table-field"] input').element)
  })

  it('takes the mark off again as soon as a table is typed', async () => {
    const view = await catalogWithOnePortion()
    await view.get('[data-test="to-review"]').trigger('click')

    await view.get('[data-test="table-field"] input').setValue('Tisch 12')

    expect(view.get('[data-test="table-field"]').classes()).not.toContain('is-missing')
  })

  it('moves on to the summary once a table is there', async () => {
    const view = await catalogWithOnePortion()
    await view.get('[data-test="table-field"] input').setValue('Tisch 12')

    await view.get('[data-test="to-review"]').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'review' })
  })
})

describe('the length of what a waiter types on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('stops the table name after forty characters', () => {
    const view = mountCatalog()

    expect(view.get('[data-test="table-input"] input').attributes('maxlength')).toBe('40')
  })
})

describe('the items screen while an order is frozen on the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(noConnection())
  })

  async function anOrderThatCouldNotBeSent() {
    useCatalogStore().catalog = CATALOG
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: 'station-bar',
      name: 'Wasser',
    })
    order.setTable('Tisch 5')
    await order.send('leaveOpen')
    return order
  }

  it('sends the waiter to the summary, where the failure and the retry are', async () => {
    await anOrderThatCouldNotBeSent()

    mountCatalog()

    expect(currentRoute.value).toEqual({ name: 'review' })
  })

  it('leaves the frozen order untouched on the way there', async () => {
    const order = await anOrderThatCouldNotBeSent()

    mountCatalog()

    expect(order.draft.lines).toHaveLength(1)
    expect(order.draft.tableName).toBe('Tisch 5')
  })

  it('stays on the items while the order still belongs to the waiter', () => {
    mountCatalog()

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})

describe('the items screen after the laptop refused an order', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(
      refusal('errors.order.cannotBeProcessed', {
        status: 400,
        code: 'ValidationFailed',
      }),
    )
  })

  it('leaves the waiter on the items, because that is where the refusal is put right', async () => {
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-wasser',
      note: null,
      stationId: null,
      name: 'Wasser',
    })
    order.setTable('Tisch 5')
    await order.send('leaveOpen')

    mountCatalog()

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})
