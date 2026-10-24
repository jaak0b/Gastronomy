import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useConnectionStore } from '../../../../src/shared/stores/connection'
import { FESTIVAL_ID, SUMMER, mountPage, festivalLaptop } from './festivalPageFixture'
import { nextTick } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { aHold, answer, heldUntil, inTurn, type StubbedLaptop } from '../../../support/laptop'

const FESTIVALS_PATH = '/api/admin/festivals'

function festivalReadsHeldBack(): { laptop: StubbedLaptop; releaseTheFirst: () => void; releaseTheNewer: () => void } {
  const firstRead = aHold()
  const newerRead = aHold()
  const festivals = answer({ festivals: [SUMMER] })
  const laptop = festivalLaptop().answers(
    'GET',
    (call) => call.url === FESTIVALS_PATH,
    inTurn(heldUntil(firstRead.released, festivals), heldUntil(newerRead.released, festivals)),
  )
  return { laptop, releaseTheFirst: firstRead.release, releaseTheNewer: newerRead.release }
}

async function letANewerReadOvertakeTheFirst(reads: ReturnType<typeof festivalReadsHeldBack>): Promise<void> {
  await vi.waitFor(() => expect(reads.laptop.urls()).toContain(FESTIVALS_PATH))
  const refetch = useConnectionStore().refetchAll()
  reads.releaseTheFirst()
  await flushPromises()
  reads.releaseTheNewer()
  await refetch
}

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('leaving the festival page', () => {
  it('offers the way back to the festivals', async () => {
    festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-name"]').exists()).toBe(true))
    expect(page.get('[data-test="back-to-festivals"]').text()).toBe('Zurück')

    await page.get('[data-test="back-to-festivals"]').trigger('click')

    expect(window.location.pathname).toBe('/admin/festivals')
  })
})

describe('the name and the dates at the top of the page', () => {
  it('carry what the laptop knows about this festival', async () => {
    festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-name"]').exists()).toBe(true))

    expect(page.get('[data-test="festival-name"]').text()).toBe('Sommerfest')
    expect(page.get('[data-test="running"]').text()).toBe('Aktiv')
    expect((page.get('[data-test="festival-name-field"] input').element as HTMLInputElement).value).toBe(
      'Sommerfest',
    )
    expect(
      (page.get('[data-test="festival-start-field"] input').element as HTMLInputElement).value.length,
    ).toBeGreaterThan(0)
  })

  it('saves the new name as soon as the admin leaves the field', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-name-field"] input').exists()).toBe(true))
    await page.get('[data-test="festival-name-field"] input').setValue('Sommerfest 2027')
    await page.get('[data-test="festival-name-field"] input').trigger('blur')

    await vi.waitFor(() => {
      const sent = laptop.calls.find((call) => call.method === 'PUT')
      expect(sent?.url).toBe(`/api/admin/festivals/${FESTIVAL_ID}`)
      expect((sent?.body as { name: string }).name).toBe('Sommerfest 2027')
    })
  })

  it('sends nothing when the admin leaves a field without changing anything', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-name-field"] input').exists()).toBe(true))
    await page.get('[data-test="festival-name-field"] input').trigger('blur')

    expect(laptop.writes()).toEqual([])
  })

  it('asks for a name when the admin empties the field', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-name-field"] input').exists()).toBe(true))
    await page.get('[data-test="festival-name-field"] input').setValue('')
    await page.get('[data-test="festival-name-field"] input').trigger('blur')
    await nextTick()

    expect(page.get('[data-test="festival-name-field"] .v-messages__message').text()).toBe(
      'Geben Sie dem Fest einen Namen.',
    )
    expect(laptop.writes()).toEqual([])
    expect((page.get('[data-test="festival-name-field"] input').element as HTMLInputElement).value).toBe('')
  })

  it('sends one change once when the admin presses enter and then leaves the field', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-name-field"] input').exists()).toBe(true))
    await page.get('[data-test="festival-name-field"] input').setValue('Sommerfest 2027')
    await page.get('[data-test="festival-name-field"] input').trigger('keyup.enter')
    await page.get('[data-test="festival-name-field"] input').trigger('blur')

    await vi.waitFor(() => expect(laptop.writes().length).toBeGreaterThan(0))
    await new Promise((carryOn) => setTimeout(carryOn, 20))
    expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
      `PUT /api/admin/festivals/${FESTIVAL_ID}`,
    ])
  })

  it('moves the admin to the overview when the laptop knows no such festival', async () => {
    festivalLaptop({ festivals: [] })

    mountPage('fest-gone')

    await vi.waitFor(() => expect(window.location.pathname).toBe('/admin/overview'))
  })

  it('stays on the festival page when a newer read of the festivals overtakes the first one', async () => {
    const reads = festivalReadsHeldBack()
    const page = mountPage()

    await letANewerReadOvertakeTheFirst(reads)

    await vi.waitFor(() => expect(page.find('[data-test="festival-name"]').exists()).toBe(true))
    expect(window.location.pathname).toBe(`/admin/festivals/${FESTIVAL_ID}`)
  })

  it('reads the table plan of the festival when a newer read of the festivals overtakes the first one', async () => {
    const reads = festivalReadsHeldBack()
    mountPage()

    await letANewerReadOvertakeTheFirst(reads)

    await vi.waitFor(() =>
      expect(reads.laptop.urls()).toContain(`/api/admin/festivals/${FESTIVAL_ID}/table-plan`),
    )
  })

  it('asks the laptop for this festival again when the connection comes back', async () => {
    const laptop = festivalLaptop()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-name"]').exists()).toBe(true))
    laptop.forgetCalls()
    await useConnectionStore().refetchAll()

    expect(laptop.calls.map((call) => call.url)).toContain(
      `/api/admin/items?festivalId=${FESTIVAL_ID}`,
    )
  })
})
