import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import ItemButton from '../../../src/components/catalog/ItemButton.vue'
import type { CatalogItem } from '../../../src/core/apiTypes'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function item(isAvailable: boolean): CatalogItem {
  return {
    id: 'item-bratwurst',
    name: 'Bratwurst',
    categoryName: 'Essen',
    priceCents: 350,
    sortOrder: 1,
    isAvailable,
    stationIds: ['station-kueche'],
  }
}

function mountButton(isAvailable: boolean, quantity: number) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(ItemButton, {
    props: { item: item(isAvailable), quantity, language: 'de' as const },
    global: { plugins: [i18n] },
  })
}

describe('ItemButton', () => {
  it('names the item and its price', () => {
    const button = mountButton(true, 0)

    expect(button.get('.name').text()).toBe('Bratwurst')
    expect(button.get('.price').text()).toBe('3,50 €')
  })

  it('adds one when the server taps an available item', async () => {
    const button = mountButton(true, 0)

    await button.get('.add').trigger('click')

    expect(button.emitted('add')).toHaveLength(1)
  })

  it('shows the count once the item is in the basket', () => {
    const button = mountButton(true, 2)

    expect(button.get('.quantity').text()).toBe('2')
  })

  it('shows no count while the item is not in the basket', () => {
    const button = mountButton(true, 0)

    expect(button.find('.quantity').exists()).toBe(false)
  })

  it('stays in the grid when it has sold out rather than disappearing', () => {
    const button = mountButton(false, 0)

    expect(button.get('.name').text()).toBe('Bratwurst')
  })

  it('says that a sold out item is sold out', () => {
    const button = mountButton(false, 0)

    expect(button.get('.sold-out').text()).toBe('Ausverkauft')
  })

  it('cannot be tapped once the item has sold out', () => {
    const button = mountButton(false, 0)

    expect(button.get('.add').attributes('disabled')).toBeDefined()
  })

  it('keeps a quantity a guest already ordered when the item sells out', () => {
    const button = mountButton(false, 3)

    expect(button.get('.quantity').text()).toBe('3')
  })

  it('takes one off again when the server taps the minus', async () => {
    const button = mountButton(true, 2)

    await button.get('.remove').trigger('click')

    expect(button.emitted('remove')).toHaveLength(1)
  })

  it('puts the minus and the count at the start of the row, ahead of the tap area', () => {
    const button = mountButton(true, 2)

    const first = button.get('.item-row').element.firstElementChild

    expect(first?.classList.contains('quantity-control')).toBe(true)
  })

  it('keeps the space for the count while the item is not in the order, so the name never moves', () => {
    const button = mountButton(true, 0)

    expect(button.find('.quantity-control').exists()).toBe(true)
    expect(button.find('.quantity').exists()).toBe(false)
  })
})
