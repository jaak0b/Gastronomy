import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import StationFulfilledCard from '../../../src/station/components/StationFulfilledCard.vue'
import type { StationOrderQueueView } from '../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../support/plugins'
import { onScreen, textOnScreen } from '../../support/dom'
import { item, stationOrder } from '../../support/station'

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
