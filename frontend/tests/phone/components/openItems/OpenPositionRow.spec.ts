import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import OpenPositionRow from '../../../../src/phone/components/openItems/OpenPositionRow.vue'
import { testPlugins } from '../../../support/plugins'

enableAutoUnmount(afterEach)

interface RowProps {
  itemName: string
  note: string | null
  priceText: string
  isSelected: boolean
  isSelectable: boolean
  isCancelled: boolean
  isShownAsPaid: boolean
}

function mountRow(overrides: Partial<RowProps> = {}) {
  const props: RowProps = {
    itemName: 'Bratwurst',
    note: null,
    priceText: '3,50 €',
    isSelected: false,
    isSelectable: true,
    isCancelled: false,
    isShownAsPaid: false,
    ...overrides,
  }
  return mount(OpenPositionRow, {
    props,
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

describe('one position of an open table', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('writes the name and the price of the position', () => {
    const row = mountRow()

    expect(row.get('[data-test="line-name"]').text()).toBe('Bratwurst')
    expect(row.get('[data-test="line-price"]').text()).toBe('3,50 €')
  })

  it('writes the note as a hint when the position carries one', () => {
    const row = mountRow({ note: 'ohne Zwiebeln' })

    expect(row.get('[data-test="line-note"]').text()).toBe('Hinweis: ohne Zwiebeln')
  })

  it('offers the tick box while the position can be selected', () => {
    const row = mountRow()

    expect(row.find('[data-test="line-tick"]').exists()).toBe(true)
    expect(row.find('[data-test="line-paid"]').exists()).toBe(false)
  })

  it('puts a paid position on a green background with the word for paid and no tick box', () => {
    const row = mountRow({ isShownAsPaid: true, isSelectable: false })

    expect(row.get('[data-test="open-line"]').classes()).toContain('paid-line')
    expect(row.get('[data-test="line-paid"]').text()).toBe('Bezahlt')
    expect(row.find('[data-test="line-tick"]').exists()).toBe(false)
  })

  it('greys a cancelled position out, strikes it through and offers no tick box', () => {
    const row = mountRow({ isCancelled: true, isSelectable: false })

    expect(row.get('[data-test="open-line"]').classes()).toEqual(
      expect.arrayContaining(['cancelled-item', 'v-list-item--disabled']),
    )
    expect(row.find('[data-test="line-tick"]').exists()).toBe(false)
  })

  it('hands the tap up so the screen decides what it means', async () => {
    const row = mountRow()

    await row.get('[data-test="open-line"]').trigger('click')

    expect(row.emitted('toggle')).toHaveLength(1)
  })
})
