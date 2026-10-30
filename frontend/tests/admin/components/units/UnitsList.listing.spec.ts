import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { iconActionLabelsOnScreen } from '../../../support/dom'
import { neverAnswers, noConnection, stubLaptop } from '../../../support/laptop'
import { OLD_KILOGRAM, unitsListLaptop, mountList } from './unitsListFixture'

describe('the units tab', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists every active unit by its name', async () => {
    unitsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('[data-test="unit-row"]').length).toBe(3))

    expect(list.findAll('[data-test="unit-row"] [data-test="name"]').map((name) => name.text())).toEqual([
      'Stück',
      'g',
      'ml',
    ])
  })

  it('leaves a deactivated unit out until the admin asks to see deactivated entries', async () => {
    unitsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('[data-test="unit-row"]').length).toBe(3))

    await list.get('[data-test="show-deactivated"] input').setValue(true)

    expect(list.findAll('[data-test="unit-row"] [data-test="name"]').map((name) => name.text())).toEqual([
      'Stück',
      'g',
      'ml',
      'kg',
    ])
  })

  it('marks a shown deactivated unit and offers to activate it', async () => {
    unitsListLaptop({ units: [OLD_KILOGRAM] })
    const list = mountList()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await vi.waitFor(() => expect(list.find('[data-test="unit-row"]').exists()).toBe(true))

    const row = list.get(`[data-test="unit-row"][data-test-id="${OLD_KILOGRAM.unitId}"]`)

    await row.get('[data-test="reactivate"]').trigger('mouseenter')

    expect(row.get('[data-test="deactivated"]').text()).toBe('Deaktiviert')
    expect(row.get('[data-test="reactivate"] .v-icon').classes()).toContain('mdi-eye')
    await vi.waitFor(() => expect(iconActionLabelsOnScreen()).toContain('Einheit aktivieren'))
  })

  it('offers the pencil and then the trash icon on a row', async () => {
    unitsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.findAll('[data-test="unit-row"]').length).toBe(3))

    const actions = list.get('[data-test="unit-row"]').findAll('[data-test="edit"], [data-test="deactivate"]')
    expect(actions.map((action) => action.attributes('data-test'))).toEqual(['edit', 'deactivate'])
  })

  it('shows the shared text of an empty list and names the button that adds a unit', async () => {
    unitsListLaptop({ units: [] })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="empty-list-text"]').exists()).toBe(true))

    expect(list.get('[data-test="empty-list-text"]').text()).toBe('Keine Einträge')
    expect(list.get('[data-test="new-unit"]').text()).toBe('Neue Einheit')
  })

  it('shows the shared text of an empty list in English', async () => {
    unitsListLaptop({ units: [] })

    const list = mountList('en')
    await vi.waitFor(() => expect(list.find('[data-test="empty-list-text"]').exists()).toBe(true))

    expect(list.get('[data-test="empty-list-text"]').text()).toBe('No entries')
  })

  it('does not claim there are no units while deactivated ones are hidden', async () => {
    const laptop = unitsListLaptop({ units: [OLD_KILOGRAM] })

    const list = mountList()
    await vi.waitFor(() => expect(laptop.urls()).toContain('/api/admin/units'))
    await flushPromises()

    expect(list.find('[data-test="empty-list-text"]').exists()).toBe(false)
  })

  it('shows no text of an empty list before the laptop has answered', async () => {
    stubLaptop().answersEverythingElse(neverAnswers())

    const list = mountList()
    await flushPromises()

    expect(list.find('[data-test="empty-list-text"]').exists()).toBe(false)
  })

  it('shows no text of an empty list after the load failed', async () => {
    stubLaptop().answersEverythingElse(noConnection())

    const list = mountList()
    await flushPromises()

    expect(list.find('[data-test="empty-list-text"]').exists()).toBe(false)
  })
})
