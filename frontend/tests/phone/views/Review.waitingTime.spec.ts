import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Review from '../../../src/phone/views/Review.vue'
import { useSessionStore } from '../../../src/shared/stores/session'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { WASSER, prepareOrder } from './reviewFixture'
import { stubLaptop, answer, type StubbedLaptop } from '../../support/laptop'

describe('the waiting time on the review screen', () => {
  function answerTheQuoteWith(readyInMinutes: number | null): StubbedLaptop {
    return stubLaptop().answersEverythingElse(
      answer({ stations: [{ stationId: 'station-bar', readyInMinutes }] }),
    )
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
    useSessionStore().deviceToken = 'token-here'
  })

  it('shows the time the laptop calculated for the station', async () => {
    answerTheQuoteWith(14)
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins() } })
    await flushPromises()

    expect(review.get('[data-test="station-name"]').text()).toBe('Geht an Bar (~14 Min.)')
  })

  it('says above the buttons how each station hands its part out and when, in German', async () => {
    answerTheQuoteWith(14)
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins() } })
    await flushPromises()

    expect({
      station: review.get('[data-test="docked-strip"] [data-test="station-delivery-name"]').text(),
      delivery: review.get('[data-test="docked-strip"] [data-test="station-delivery-mode"]').text(),
    }).toEqual({ station: 'Bar:', delivery: 'Gemeinsam (~14 Min.)' })
  })

  it('says above the buttons how each station hands its part out and when, in English', async () => {
    answerTheQuoteWith(14)
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins('en') } })
    await flushPromises()

    expect({
      station: review.get('[data-test="docked-strip"] [data-test="station-delivery-name"]').text(),
      delivery: review.get('[data-test="docked-strip"] [data-test="station-delivery-mode"]').text(),
    }).toEqual({ station: 'Bar:', delivery: 'Combined (~14 min)' })
  })

  it('shows no time for a station the laptop could not calculate', async () => {
    answerTheQuoteWith(null)
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins() } })
    await flushPromises()

    expect(review.get('[data-test="station-name"]').text()).toBe('Geht an Bar')
  })

  it('asks again with the new count when the order changes', async () => {
    const laptop = answerTheQuoteWith(14)
    const order = prepareOrder()
    mount(Review, { global: { plugins: testPlugins() } })
    await flushPromises()

    order.addItem({ catalogItemId: WASSER.id, note: 'ohne Eis', stationId: 'station-bar', name: WASSER.name })
    await flushPromises()

    expect(laptop.writtenBodies().at(-1)).toEqual({
      lines: [{ catalogItemId: 'item-wasser', stationId: 'station-bar', units: 2 }],
    })
  })
})
