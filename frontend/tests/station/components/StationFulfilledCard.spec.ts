import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import StationFulfilledCard from '../../../src/station/components/StationFulfilledCard.vue'
import type { StationOrderQueueView } from '../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../support/plugins'
import { onScreen, textOnScreen } from '../../support/dom'
import { cancelledItem, item, stationOrder } from '../../support/station'

const HANDED_OUT = '2026-09-05T18:30:00Z'

function mountDoneCard(order: StationOrderQueueView, isWorking = false) {
  return mount(StationFulfilledCard, {
    props: { stationOrder: order, isWorking },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

describe('a card in the done view', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('shows the note of a done item, so a mistaken tap is put back on the right one', () => {
    mountDoneCard(
      stationOrder({
        fulfilledItemCount: 1,
        items: [{ ...item('a', 'Bratwurst', HANDED_OUT), note: 'ohne Senf' }, item('b', 'Pommes')],
      }),
    )

    const note = textOnScreen('[data-test="station-item"][data-test-id="a"] [data-test="item-note"]')

    expect(note.trim()).toBe('Hinweis: ohne Senf')
  })

  it('keeps the put back control off while another tap is still on its way to the laptop', () => {
    mountDoneCard(
      stationOrder({ fulfilledItemCount: 1, items: [item('a', 'Bratwurst', HANDED_OUT), item('b', 'Pommes')] }),
      true,
    )

    const putBack = onScreen('[data-test="station-item"][data-test-id="a"] [data-test="put-back"]')

    expect(putBack.hasAttribute('disabled')).toBe(true)
  })
})

describe('a done card with an item a waiter cancelled', () => {
  const WITH_CANCELLED_POMMES = stationOrder({
    itemCount: 1,
    fulfilledItemCount: 1,
    items: [item('a', 'Bratwurst', HANDED_OUT), cancelledItem('b', 'Pommes')],
  })

  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('shows the cancelled item struck through', () => {
    mountDoneCard(WITH_CANCELLED_POMMES)

    expect(onScreen('[data-test="station-item"][data-test-id="b"] [data-test="item-name"]').classList).toContain('cancelled-item')
  })

  it('says when the item was cancelled', () => {
    mountDoneCard(WITH_CANCELLED_POMMES)

    expect(textOnScreen('[data-test="station-item"][data-test-id="b"] [data-test="cancelled-at"]').trim()).toBe('Storniert um 20:20')
  })

  it('says when the station confirmed a cancellation', () => {
    mountDoneCard(
      stationOrder({
        itemCount: 0,
        items: [{ ...cancelledItem('b', 'Pommes'), state: 'cancellationAcknowledged', stateSetAtUtc: '2026-09-05T18:25:00Z' }],
      }),
    )

    expect(textOnScreen('[data-test="station-item"][data-test-id="b"] [data-test="cancelled-at"]').trim()).toBe(
      'Stornierung bestätigt um 20:25',
    )
  })

  it('offers neither a tick nor a put back control on the cancelled item', () => {
    mountDoneCard(WITH_CANCELLED_POMMES)

    expect(document.querySelector('[data-test="station-item"][data-test-id="b"] [data-test="item-tick"]')).toBeNull()
    expect(document.querySelector('[data-test="station-item"][data-test-id="b"] [data-test="put-back"]')).toBeNull()
  })

  it('leaves the cancelled item out of the summary line', () => {
    mountDoneCard(WITH_CANCELLED_POMMES)

    expect(textOnScreen('[data-test="unit-summary"]')).toBe('1 x Bratwurst')
  })
})
