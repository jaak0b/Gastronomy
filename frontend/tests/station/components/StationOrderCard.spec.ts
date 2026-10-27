import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import StationOrderCard from '../../../src/station/components/StationOrderCard.vue'
import type { StationOrderQueueView } from '../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../support/plugins'
import { clickOn, isOnScreen, onScreen, textOnScreen } from '../../support/dom'
import { cancelledItem, item, stationOrder } from '../../support/station'

interface CardSettings {
  selectedItemIds?: string[]
  isWorking?: boolean
  showHide?: boolean
}

function mountCard(order: StationOrderQueueView, settings: CardSettings = {}) {
  return mount(StationOrderCard, {
    props: {
      stationOrder: order,
      selectedItemIds: settings.selectedItemIds ?? [],
      isWorking: settings.isWorking ?? false,
      showHide: settings.showHide ?? true,
    },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

function groupedLines(): string[] {
  return [...document.querySelectorAll('[data-test="grouped-line"]')].map(
    (line) => line.textContent?.trim() ?? '',
  )
}

describe('a station card with items selected on it', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('hands on only its own selected items, not those selected on another card', async () => {
    const card = mountCard(stationOrder(), { selectedItemIds: ['b', 'item-on-another-card'] })

    await clickOn('[data-test="fulfill"]')

    expect(card.emitted('fulfill')?.[0]?.[1]).toEqual(['b'])
  })

  it('keeps the done control off while another tap is still on its way to the laptop', () => {
    mountCard(stationOrder(), { selectedItemIds: ['a'], isWorking: true })

    const fulfill = onScreen('[data-test="fulfill"]')

    expect(fulfill.hasAttribute('disabled')).toBe(true)
  })

  it('reports a tap on an item so the page can select it', async () => {
    const card = mountCard(stationOrder())

    await clickOn('[data-test="station-item"][data-test-id="b"]')

    expect(card.emitted('toggleItem')).toEqual([['b']])
  })
})

describe('an as-it-comes card the employee wants out of the second column', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('names the order to hide', async () => {
    const card = mountCard(stationOrder({ stationOrderId: 'station-order-7', deliveryMode: 'asItComes' }))

    await clickOn('[data-test="hide"]')

    expect(card.emitted('hide')).toEqual([['station-order-7']])
  })

  it('keeps the hide control off while another tap is still on its way to the laptop', () => {
    mountCard(stationOrder({ deliveryMode: 'asItComes' }), { isWorking: true })

    const hide = onScreen('[data-test="hide"]')

    expect(hide.hasAttribute('disabled')).toBe(true)
  })
})

describe('a card switched to the grouped view', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('counts equal items together and keeps an item with a note on a line of its own', async () => {
    mountCard(
      stationOrder({
        itemCount: 3,
        items: [
          item('a', 'Bratwurst'),
          { ...item('b', 'Bratwurst'), note: 'ohne Senf' },
          item('c', 'Bratwurst'),
        ],
      }),
    )

    await clickOn('[data-test="grouped-toggle"]')

    expect(groupedLines()).toEqual(['2 x Bratwurst', '1 x Bratwurst · Hinweis: ohne Senf'])
  })

  it('leaves items that are already done out of the count', async () => {
    mountCard(
      stationOrder({
        itemCount: 3,
        fulfilledItemCount: 1,
        items: [
          item('a', 'Bratwurst'),
          item('b', 'Bratwurst', '2026-09-05T18:10:00Z'),
          item('c', 'Pommes'),
        ],
      }),
    )

    await clickOn('[data-test="grouped-toggle"]')

    expect(groupedLines()).toEqual(['1 x Bratwurst', '1 x Pommes'])
  })
})

describe('a card with an item a waiter cancelled while it was still open', () => {
  const WITH_CANCELLED_POMMES = stationOrder({
    itemCount: 1,
    items: [item('a', 'Bratwurst'), cancelledItem('b', 'Pommes')],
  })

  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('shows the cancelled item struck through', () => {
    mountCard(WITH_CANCELLED_POMMES)

    const pommes = onScreen('[data-test="station-item"][data-test-id="b"] [data-test="item-name"]')

    expect(pommes.classList).toContain('cancelled-item')
    expect(onScreen('[data-test="station-item"][data-test-id="a"] [data-test="item-name"]').classList).not.toContain('cancelled-item')
  })

  it('says when the item was cancelled', () => {
    mountCard(WITH_CANCELLED_POMMES)

    expect(textOnScreen('[data-test="station-item"][data-test-id="b"] [data-test="cancelled-at"]').trim()).toBe('Storniert um 20:20')
  })

  it('reports a tap on the cancelled item so the page can select it', async () => {
    const card = mountCard(WITH_CANCELLED_POMMES)

    await clickOn('[data-test="station-item"][data-test-id="b"]')

    expect(card.emitted('toggleItem')).toEqual([['b']])
  })

  it('offers the confirmation instead of the done control once the cancelled item is selected', () => {
    mountCard(WITH_CANCELLED_POMMES, { selectedItemIds: ['b'] })

    expect(textOnScreen('[data-test="acknowledge"]').trim()).toBe('Stornierung bestätigen')
    expect(isOnScreen('[data-test="fulfill"]')).toBe(false)
  })

  it('confirms only the selected cancelled items of this card', async () => {
    const card = mountCard(WITH_CANCELLED_POMMES, { selectedItemIds: ['a', 'b', 'item-on-another-card'] })

    await clickOn('[data-test="acknowledge"]')

    expect(card.emitted('acknowledge')).toEqual([[['b']]])
  })

  it('keeps the confirmation off while another tap is still on its way to the laptop', () => {
    mountCard(WITH_CANCELLED_POMMES, { selectedItemIds: ['b'], isWorking: true })

    expect(onScreen('[data-test="acknowledge"]').hasAttribute('disabled')).toBe(true)
  })

  it('keeps a cancelled bratwurst on a struck-through line of its own in the grouped view', async () => {
    mountCard(
      stationOrder({
        itemCount: 2,
        items: [item('a', 'Bratwurst'), item('b', 'Bratwurst'), cancelledItem('c', 'Bratwurst')],
      }),
    )

    await clickOn('[data-test="grouped-toggle"]')

    expect(groupedLines()).toEqual(['2 x Bratwurst', '1 x Bratwurst'])
    expect(
      [...document.querySelectorAll('[data-test="grouped-line"]')].map((line) =>
        line.classList.contains('cancelled-item'),
      ),
    ).toEqual([false, true])
  })
})
