import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import OpenPositionRow from '../../../../src/phone/components/openItems/OpenPositionRow.vue'
import type { PositionState } from '../../../../src/phone/core/openItems'
import { testPlugins } from '../../../support/plugins'

enableAutoUnmount(afterEach)

interface RowProps {
  itemName: string
  note: string | null
  priceText: string
  isSelected: boolean
  isSelectable: boolean
  isSettled: boolean
  productionState: PositionState
}

function mountRow(overrides: Partial<RowProps> = {}) {
  const props: RowProps = {
    itemName: 'Bratwurst',
    note: null,
    priceText: '3,50 €',
    isSelected: false,
    isSelectable: true,
    isSettled: false,
    productionState: 'notProduced',
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

  it('shows a tick for a position the station has handed out', () => {
    const row = mountRow({ productionState: 'produced' })

    expect(row.get('[data-test="open-line"]').classes()).toContain('is-produced')
    expect(row.get('[data-test="line-state"]').classes()).toContain('mdi-check')
  })

  it('shows a clock for a position still at the station', () => {
    const row = mountRow({ productionState: 'notProduced' })

    expect(row.get('[data-test="open-line"]').classes()).toContain('is-not-produced')
    expect(row.get('[data-test="line-state"]').classes()).toContain('mdi-clock-outline')
  })

  it('offers the tick box while the position is open', () => {
    const row = mountRow()

    expect(row.find('[data-test="line-tick"]').exists()).toBe(true)
    expect(row.find('[data-test="line-paid"]').exists()).toBe(false)
  })

  it('writes the word for paid instead of a tick box once the position is settled', () => {
    const row = mountRow({ isSettled: true, isSelectable: false })

    expect(row.get('[data-test="line-paid"]').text()).toBe('Bezahlt')
    expect(row.find('[data-test="line-tick"]').exists()).toBe(false)
  })

  it('strikes a cancelled position through and offers no tick box and no state for it', () => {
    const row = mountRow({ productionState: 'cancelled', isSelectable: false })

    expect(row.get('[data-test="open-line"]').classes()).toContain('cancelled-item')
    expect(row.find('[data-test="line-tick"]').exists()).toBe(false)
    expect(row.find('[data-test="line-state"]').exists()).toBe(false)
  })

  it('offers the tick box for a paid position the screen lets the waiter select', () => {
    const row = mountRow({ isSettled: true, isSelectable: true })

    expect(row.find('[data-test="line-tick"]').exists()).toBe(true)
    expect(row.get('[data-test="line-paid"]').text()).toBe('Bezahlt')
  })

  it('hands the tap up so the screen decides what it means', async () => {
    const row = mountRow()

    await row.get('[data-test="open-line"]').trigger('click')

    expect(row.emitted('toggle')).toHaveLength(1)
  })
})
