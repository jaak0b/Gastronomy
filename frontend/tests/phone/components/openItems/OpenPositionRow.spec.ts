import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import OpenPositionRow from '../../../../src/phone/components/openItems/OpenPositionRow.vue'
import type { PositionState } from '../../../../src/phone/core/openItems'
import { testPlugins } from '../../../support/plugins'

enableAutoUnmount(afterEach)

interface RowProps {
  itemName: string
  note: string | null
  orderLabel: string | null
  priceText: string
  isSelected: boolean
  isDisabled: boolean
  isSettled: boolean
  productionState: PositionState
}

function mountRow(overrides: Partial<RowProps> = {}) {
  const props: RowProps = {
    itemName: 'Bratwurst',
    note: null,
    orderLabel: null,
    priceText: '3,50 €',
    isSelected: false,
    isDisabled: false,
    isSettled: false,
    productionState: 'unknown',
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

    expect(row.get('.line-name').text()).toBe('Bratwurst')
    expect(row.get('.line-price').text()).toBe('3,50 €')
  })

  it('writes the order it came from where the screen hands one in', () => {
    const row = mountRow({ orderLabel: 'Bestellung 137' })

    expect(row.get('.line-origin').text()).toBe('Bestellung 137')
  })

  it('leaves the order out where the screen already names it', () => {
    const row = mountRow()

    expect(row.find('.line-origin').exists()).toBe(false)
  })

  it('writes the note as a hint when the position carries one', () => {
    const row = mountRow({ note: 'ohne Zwiebeln' })

    expect(row.get('.line-note').text()).toBe('Hinweis: ohne Zwiebeln')
  })

  it('shows a tick for a position the station has handed out', () => {
    const row = mountRow({ productionState: 'produced' })

    expect(row.get('.open-line').classes()).toContain('is-produced')
    expect(row.get('.line-state').classes()).toContain('mdi-check')
  })

  it('shows a clock for a position still at the station', () => {
    const row = mountRow({ productionState: 'notProduced' })

    expect(row.get('.open-line').classes()).toContain('is-not-produced')
    expect(row.get('.line-state').classes()).toContain('mdi-clock-outline')
  })

  it('shows no state at all where the screen does not know it', () => {
    const row = mountRow()

    expect(row.get('.open-line').classes()).not.toContain('is-produced')
    expect(row.get('.open-line').classes()).not.toContain('is-not-produced')
    expect(row.find('.line-state').exists()).toBe(false)
  })

  it('offers the tick box while the position is open', () => {
    const row = mountRow()

    expect(row.find('.line-tick').exists()).toBe(true)
    expect(row.find('.line-paid').exists()).toBe(false)
  })

  it('writes the word for paid instead of a tick box once the position is settled', () => {
    const row = mountRow({ isSettled: true })

    expect(row.get('.line-paid').text()).toBe('Bezahlt')
    expect(row.find('.line-tick').exists()).toBe(false)
  })

  it('hands the tap up so the screen decides what it means', async () => {
    const row = mountRow()

    await row.get('.open-line').trigger('click')

    expect(row.emitted('toggle')).toHaveLength(1)
  })
})
