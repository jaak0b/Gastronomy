import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h, ref } from 'vue'
import { useStationChoiceQuotes } from '../../../src/phone/composables/useStationChoiceQuotes'
import { useEstimatesStore } from '../../../src/phone/stores/estimates'
import { useSessionStore } from '../../../src/shared/stores/session'
import { CatalogItemView } from '../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../support/plugins'
import { answer, neverAnswers, stubLaptop } from '../../support/laptop'

const KAFFEE: CatalogItemView = {
  id: 'item-kaffee',
  name: 'Kaffee',
  categoryId: 'category-essen',
  priceCents: 250,
  sortOrder: 3,
  isAvailable: true,
  stationIds: ['station-kueche', 'station-bar'],
  productionMinutes: 10,
  isQueueIndependent: false,
}

function answerEachQuoteWith(minutesAt: Record<string, number | null>): void {
  stubLaptop()
    .answersEverythingElse(answer([]))
    .answers('ANY', (call) => call.url === '/api/estimates/quote', (call) => {
      const { lines } = call.body as { lines: { catalogItemId: string; stationId: string }[] }
      const stationId = lines.find((line) => line.catalogItemId === 'item-kaffee')?.stationId ?? ''
      return answer({ stations: [{ stationId, readyInMinutes: minutesAt[stationId] ?? null }] })(call)
    })
}

function mountTheStationQuestion() {
  const itemBehindTheQuestion = ref<CatalogItemView | null>(null)
  const linesAwaitingStation = ref<number[]>([])
  let estimateFor: (stationId: string) => number | null = () => null
  const host = mount(
    defineComponent({
      setup() {
        estimateFor = useStationChoiceQuotes(itemBehindTheQuestion, linesAwaitingStation)
          .estimateForTheStationChoice
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins() } },
  )
  return { host, itemBehindTheQuestion, estimateFor: (stationId: string) => estimateFor(stationId) }
}

describe('the waiting times offered in the question about the station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('gives each station the time the laptop quoted for it', async () => {
    answerEachQuoteWith({ 'station-kueche': 25, 'station-bar': 70 })
    const question = mountTheStationQuestion()

    question.itemBehindTheQuestion.value = KAFFEE
    await flushPromises()

    expect([question.estimateFor('station-kueche'), question.estimateFor('station-bar')]).toEqual([25, 70])
  })

  it('gives no time while the laptop has not answered yet', async () => {
    stubLaptop()
      .answersEverythingElse(answer([]))
      .answers('ANY', (call) => call.url === '/api/estimates/quote', neverAnswers())
    const question = mountTheStationQuestion()

    question.itemBehindTheQuestion.value = KAFFEE
    await flushPromises()

    expect([question.estimateFor('station-kueche'), question.estimateFor('station-bar')]).toEqual([null, null])
  })

  it('gives no time to a station the laptop quoted no time for', async () => {
    answerEachQuoteWith({ 'station-kueche': 25, 'station-bar': null })
    const question = mountTheStationQuestion()

    question.itemBehindTheQuestion.value = KAFFEE
    await flushPromises()

    expect([question.estimateFor('station-kueche'), question.estimateFor('station-bar')]).toEqual([25, null])
  })

  it('gives no time at all when the item has just sold out', async () => {
    answerEachQuoteWith({ 'station-kueche': 25, 'station-bar': 70 })
    const question = mountTheStationQuestion()

    question.itemBehindTheQuestion.value = { ...KAFFEE, isAvailable: false }
    await flushPromises()

    expect([question.estimateFor('station-kueche'), question.estimateFor('station-bar')]).toEqual([null, null])
  })

  it('forgets the quoted times once the screen asking the question is gone', async () => {
    answerEachQuoteWith({ 'station-kueche': 25, 'station-bar': 70 })
    const question = mountTheStationQuestion()
    question.itemBehindTheQuestion.value = KAFFEE
    await flushPromises()

    question.host.unmount()

    expect(useEstimatesStore().stationChoiceQuotes).toEqual({})
  })
})
