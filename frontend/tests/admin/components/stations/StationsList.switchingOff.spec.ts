import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { pressInDialog, waitForDialog } from '../../../support/dom'
import { stubLaptop, answer } from '../../../support/laptop'
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

  it('offers to switch it back on without asking a question first', async () => {
    stubLaptop().answersEverythingElse(answer(ONE_STATION_SWITCHED_OFF))

    const list = mountList()
    await flushPromises()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await vi.waitFor(() => expect(list.find('[data-test="station-row"]').exists()).toBe(true))

    expect(list.get('[data-test="reactivate"]').text()).toBe('Ausgabestelle einschalten')
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
      '2 Artikel hätten dann keine Ausgabestelle mehr. Ordnen Sie sie zuerst einer anderen Ausgabestelle zu oder entfernen Sie sie vom Fest.',
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
      '1 Artikel hätte dann keine Ausgabestelle mehr. Ordnen Sie ihn zuerst einer anderen Ausgabestelle zu oder entfernen Sie ihn vom Fest.',
    )
  })

  it('takes the plural form when the laptop wrote the number as text', async () => {
    refuseDeactivationWith('errors.admin.stations.itemsWouldHaveNoStation', { count: '2' })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('[data-test="refusal"]').text()).toBe(
      '2 Artikel hätten dann keine Ausgabestelle mehr. Ordnen Sie sie zuerst einer anderen Ausgabestelle zu oder entfernen Sie sie vom Fest.',
    )
  })

  it('falls back to a general message for a reason this app does not know', async () => {
    refuseDeactivationWith('admin.somethingAddedLater', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('[data-test="refusal"]').text()).toBe(
      'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
    )
  })
})
