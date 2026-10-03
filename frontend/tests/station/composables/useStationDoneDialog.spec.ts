import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h } from 'vue'
import { useStationDoneDialog, type StationDoneDialogState } from '../../../src/station/composables/useStationDoneDialog'
import { testPlugins } from '../../support/plugins'
import { aHold, heldUntil, stubLaptopAt, type Hold } from '../../support/laptop'
import { aQueue, enrolledStationTablet, item, stationOrder } from '../../support/station'

const HANDED_OUT = '2026-09-05T18:10:00Z'

function mountTheQuestion(): StationDoneDialogState {
  const states: StationDoneDialogState[] = []
  mount(
    defineComponent({
      setup() {
        states.push(useStationDoneDialog())
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins() } },
  )
  return states[0]
}

describe('the done question on a station tablet', () => {
  let heldAnswer: Hold | null = null

  beforeEach(() => {
    setActivePinia(createPinia())
    enrolledStationTablet()
  })

  afterEach(async () => {
    heldAnswer?.release()
    await flushPromises()
    heldAnswer = null
    vi.unstubAllGlobals()
  })

  it('closes before the laptop has answered, so a second tap cannot send it again', async () => {
    heldAnswer = aHold()
    stubLaptopAt({ '/api/station/items/fulfill': heldUntil(heldAnswer.released, aQueue([])) })
    const question = mountTheQuestion()
    question.openDone(stationOrder(), ['a'])

    void question.confirmDone()
    await flushPromises()

    expect(question.doneStationOrder.value).toBeNull()
  })

  it('sends exactly the items it was opened with', async () => {
    const laptop = stubLaptopAt({ '/api/station/items/fulfill': aQueue([]) })
    const question = mountTheQuestion()
    question.openDone(stationOrder({ items: [item('a', 'Bratwurst'), item('b', 'Pommes')] }), ['b'])

    await question.confirmDone()

    expect(laptop.writtenBodies()).toEqual([{ orderItemIds: ['b'] }])
  })

  it('lists only the chosen items that are still open', () => {
    stubLaptopAt({})
    const question = mountTheQuestion()

    question.openDone(
      stationOrder({
        itemCount: 3,
        fulfilledItemCount: 1,
        items: [item('a', 'Bratwurst'), item('b', 'Bratwurst', HANDED_OUT), item('c', 'Pommes')],
      }),
      ['a', 'b'],
    )

    expect(question.doneUnits.value).toEqual([
      expect.objectContaining({ itemName: 'Bratwurst', note: null, units: 1 }),
    ])
  })

  it('sends nothing when the employee backs out', async () => {
    const laptop = stubLaptopAt({ '/api/station/items/fulfill': aQueue([]) })
    const question = mountTheQuestion()
    question.openDone(stationOrder(), ['a'])

    question.closeDone()
    await flushPromises()

    expect(laptop.writes()).toEqual([])
  })

  it('closes when the employee backs out', () => {
    stubLaptopAt({})
    const question = mountTheQuestion()
    question.openDone(stationOrder(), ['a'])

    question.closeDone()

    expect(question.doneStationOrder.value).toBeNull()
  })
})
