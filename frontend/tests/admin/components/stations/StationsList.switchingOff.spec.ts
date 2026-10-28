import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { iconActionLabelsOnScreen, pressInDialog, refusalReasonsOnScreen, waitForDialog } from '../../../support/dom'
import { stubLaptop, answer } from '../../../support/laptop'
import { anAdminStation } from '../../../support/wireViews'
import { STATION_ID, ONE_STATION, refuseDeactivationWith, mountList, deactivateFirstStation, ONE_STATION_SWITCHED_OFF } from './stationsListFixture'

describe('switching a station off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('asks before it happens, rather than acting on the first tap', async () => {
    const laptop = stubLaptop().answersEverythingElse(answer(ONE_STATION))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="deactivate"]').trigger('click')

    await waitForDialog()

    expect(laptop.urls().some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('does nothing when the question is answered with no', async () => {
    const laptop = stubLaptop().answersEverythingElse(answer(ONE_STATION))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="deactivate"]').trigger('click')
    await pressInDialog('[data-test="cancel"]')

    expect(laptop.urls().some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('switches the station off once the question is answered with yes', async () => {
    const laptop = stubLaptop().answersEverythingElse(answer(ONE_STATION))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="deactivate"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/stations/${STATION_ID}/deactivate`),
    )
  })
})

const ITEMS_TO_HAND_OUT = 'Solange ein Fest aktiv ist, kann eine Ausgabestelle mit offenen Bestellungen nicht deaktiviert werden.'

function stationsLaptop(station: Parameters<typeof anAdminStation>[0]) {
  stubLaptop().answersEverythingElse(answer({ stations: [anAdminStation({ stationId: STATION_ID, ...station })] }))
}

async function pointAtTheTrashIcon(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('[data-test="deactivate-wrapper"]').exists()).toBe(true))
  await list.get('[data-test="deactivate-wrapper"]').trigger('mouseenter')
}

describe('a station the laptop would refuse to switch off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('cannot be switched off while it still has items to hand out at the active festival', async () => {
    stationsLaptop({ hasItemsToHandOutAtTheRunningFestival: true })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))

    expect((list.get('[data-test="deactivate"]').element as HTMLButtonElement).disabled).toBe(true)
  })

  it('names the open orders as the reason when the admin points at the trash icon', async () => {
    stationsLaptop({ hasItemsToHandOutAtTheRunningFestival: true })
    const list = mountList()

    await pointAtTheTrashIcon(list)

    await vi.waitFor(() => expect(refusalReasonsOnScreen()).toEqual([ITEMS_TO_HAND_OUT]))
  })

  it('names how many articles would be left without a station', async () => {
    stationsLaptop({ strandedItemCountWhenSwitchedOff: 2 })
    const list = mountList()

    await pointAtTheTrashIcon(list)

    await vi.waitFor(() =>
      expect(refusalReasonsOnScreen()).toEqual([
        '2 Artikel hätten dann keine Ausgabestelle mehr.',
      ]),
    )
  })

  it('names the open orders when both reasons apply, because the laptop refuses for those first', async () => {
    stationsLaptop({ hasItemsToHandOutAtTheRunningFestival: true, strandedItemCountWhenSwitchedOff: 1 })
    const list = mountList()

    await pointAtTheTrashIcon(list)

    await vi.waitFor(() => expect(refusalReasonsOnScreen()).toEqual([ITEMS_TO_HAND_OUT]))
  })
})

describe('a station that is switched off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('is left out of the list until the admin asks to see deactivated entries', async () => {
    stubLaptop().answersEverythingElse(answer(ONE_STATION_SWITCHED_OFF))

    const list = mountList()
    await flushPromises()

    expect(list.find('[data-test="station-row"]').exists()).toBe(false)
  })

  it('says so on its row once it is shown', async () => {
    stubLaptop().answersEverythingElse(answer(ONE_STATION_SWITCHED_OFF))

    const list = mountList()
    await flushPromises()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))

    expect(list.get('[data-test="deactivated"]').text()).toBe('Deaktiviert')
  })

  it('offers to switch it back on through the open eye, named "Ausgabestelle aktivieren"', async () => {
    stubLaptop().answersEverythingElse(answer(ONE_STATION_SWITCHED_OFF))
    const list = mountList()
    await flushPromises()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))

    await list.get('[data-test="reactivate"]').trigger('mouseenter')

    expect(list.get('[data-test="reactivate"] .v-icon').classes()).toContain('mdi-eye')
    await vi.waitFor(() => expect(iconActionLabelsOnScreen()).toContain('Ausgabestelle aktivieren'))
  })

  it('switches it back on at its own address', async () => {
    const laptop = stubLaptop().answersEverythingElse(answer(ONE_STATION_SWITCHED_OFF))

    const list = mountList()
    await flushPromises()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="reactivate"]').trigger('click')

    await vi.waitFor(() => expect(laptop.urls()).toContain(`/api/admin/stations/${STATION_ID}/activate`))
  })
})

describe('the buttons beside a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('switch that station off at its own address', async () => {
    const laptop = stubLaptop().answersEverythingElse(answer(ONE_STATION))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))
    await list.get('[data-test="deactivate"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/stations/${STATION_ID}/deactivate`),
    )
  })

})

describe('a station the laptop refuses to switch off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('says items would be left with no station when that is the reason', async () => {
    refuseDeactivationWith('errors.admin.stations.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('[data-test="refusal"]').text()).toBe(
      '2 Artikel hätten dann keine Ausgabestelle mehr. Ordnen Sie sie zuerst einer anderen Ausgabestelle zu.',
    )
  })

  it('never blames unfinished orders for an orphaned-items refusal', async () => {
    refuseDeactivationWith('errors.admin.stations.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('[data-test="refusal"]').text()).not.toContain('offene Bestellungen')
  })

  it('takes the singular form when a single item would be left behind', async () => {
    refuseDeactivationWith('errors.admin.stations.itemsWouldHaveNoStation', { count: 1 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('[data-test="refusal"]').text()).toBe(
      '1 Artikel hätte dann keine Ausgabestelle mehr. Ordnen Sie ihn zuerst einer anderen Ausgabestelle zu.',
    )
  })

  it('takes the plural form when the laptop wrote the number as text', async () => {
    refuseDeactivationWith('errors.admin.stations.itemsWouldHaveNoStation', { count: '2' })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('[data-test="refusal"]').text()).toBe(
      '2 Artikel hätten dann keine Ausgabestelle mehr. Ordnen Sie sie zuerst einer anderen Ausgabestelle zu.',
    )
  })

  it('falls back to a general message for a reason this app does not know', async () => {
    refuseDeactivationWith('admin.somethingAddedLater', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('[data-test="refusal"]').text()).toBe(
      'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal oder laden Sie die Seite neu.',
    )
  })
})
