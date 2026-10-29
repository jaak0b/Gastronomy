import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import type { AdminCategoryView } from '../../../src/shared/api/generatedSchemas'
import CategoryLabel from '../../../src/shared/components/CategoryLabel.vue'

function category(colourHex: string): AdminCategoryView {
  const food: AdminCategoryView = { categoryId: 'category-food', name: 'Speisen', colourHex, sortOrder: 1, isActive: true }
  return food
}

describe('the coloured name of a category', () => {
  it('shows the name as a heading at the level its screen asks for', () => {
    const label = mount(CategoryLabel, { props: { category: category('#FFEE58'), headingLevel: 3 } })

    expect(label.element.tagName).toBe('H3')
    expect(label.text()).toBe('Speisen')
  })

  it('paints a light colour with dark lettering', () => {
    const label = mount(CategoryLabel, { props: { category: category('#FFEE58'), headingLevel: 2 } })

    expect(label.element.tagName).toBe('H2')
    expect(label.attributes('style')).toContain('background-color: rgb(255, 238, 88)')
    expect(label.attributes('style')).toContain('color: rgb(0, 0, 0)')
  })

  it('paints a dark colour with light lettering', () => {
    const label = mount(CategoryLabel, { props: { category: category('#1A237E'), headingLevel: 2 } })

    expect(label.attributes('style')).toContain('background-color: rgb(26, 35, 126)')
    expect(label.attributes('style')).toContain('color: rgb(255, 255, 255)')
  })
})
