import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { iconActionLabelsOnScreen } from '../../../support/dom'
import { BUN, OLD_MILK, ingredientsListLaptop, mountList } from './ingredientsListFixture'

describe('the ingredients tab', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists every active ingredient with the word for its unit', async () => {
    ingredientsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('[data-test="ingredient-row"]').length).toBe(2))

    expect(list.findAll('[data-test="ingredient-row"] [data-test="name"]').map((name) => name.text())).toEqual([
      'Brötchen',
      'Senf',
    ])
    expect(list.findAll('[data-test="ingredient-row"] [data-test="unit"]').map((unit) => unit.text())).toEqual([
      'Stück',
      'g',
    ])
  })

  it('shows the unit name as the data it is, also in English', async () => {
    ingredientsListLaptop({ ingredients: [BUN] })

    const list = mountList('en')
    await vi.waitFor(() => expect(list.find('[data-test="ingredient-row"]').exists()).toBe(true))

    expect(list.get('[data-test="unit"]').text()).toBe('Stück')
  })

  it('leaves a deactivated ingredient out until the admin asks to see deactivated entries', async () => {
    ingredientsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('[data-test="ingredient-row"]').length).toBe(2))

    await list.get('[data-test="show-deactivated"] input').setValue(true)

    expect(list.findAll('[data-test="ingredient-row"] [data-test="name"]').map((name) => name.text())).toEqual([
      'Brötchen',
      'Senf',
      'Milch',
    ])
  })

  it('marks a shown deactivated ingredient and offers to activate it', async () => {
    ingredientsListLaptop({ ingredients: [OLD_MILK] })
    const list = mountList()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await vi.waitFor(() => expect(list.find('[data-test="ingredient-row"]').exists()).toBe(true))

    const row = list.get(`[data-test="ingredient-row"][data-test-id="${OLD_MILK.ingredientId}"]`)

    await row.get('[data-test="reactivate"]').trigger('mouseenter')

    expect(row.get('[data-test="deactivated"]').text()).toBe('Deaktiviert')
    expect(row.get('[data-test="reactivate"] .v-icon').classes()).toContain('mdi-eye')
    await vi.waitFor(() => expect(iconActionLabelsOnScreen()).toContain('Zutat aktivieren'))
  })

  it('offers the pencil and then the trash icon on a row', async () => {
    ingredientsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('[data-test="ingredient-row"]').length).toBe(2))

    const actions = list.get('[data-test="ingredient-row"]').findAll('[data-test="edit"], [data-test="deactivate"]')
    expect(actions.map((action) => action.attributes('data-test'))).toEqual(['edit', 'deactivate'])
  })

  it('says there are no ingredients yet and names the button that adds one', async () => {
    ingredientsListLaptop({ ingredients: [] })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="none-yet"]').exists()).toBe(true))

    expect(list.get('[data-test="none-yet"]').text()).toBe(
      'Es gibt noch keine Zutaten. Klicken Sie auf "Neue Zutat".',
    )
    expect(list.get('[data-test="new-ingredient"]').text()).toBe('Neue Zutat')
  })

  it('says there are no ingredients yet in English', async () => {
    ingredientsListLaptop({ ingredients: [] })

    const list = mountList('en')
    await vi.waitFor(() => expect(list.find('[data-test="none-yet"]').exists()).toBe(true))

    expect(list.get('[data-test="none-yet"]').text()).toBe(
      'There are no ingredients yet. Click "New ingredient".',
    )
  })

  it('does not claim there are no ingredients while deactivated ones are hidden', async () => {
    const laptop = ingredientsListLaptop({ ingredients: [OLD_MILK] })

    const list = mountList()
    await vi.waitFor(() => expect(laptop.urls()).toContain('/api/admin/ingredients'))
    await flushPromises()

    expect(list.find('[data-test="none-yet"]').exists()).toBe(false)
  })
})
