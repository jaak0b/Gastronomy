import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { refusal } from '../../support/laptop'
import { stationLaptop, mountPage } from './stationPageFixture'

describe('a station tablet that has lost contact with the laptop', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stationLaptop({
      orders: () => {
        throw new TypeError('Failed to fetch')
      },
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the list may be out of date, so nobody trusts an empty screen', async () => {
    const page = await mountPage()

    expect(page.get('[data-test="load-failed"]').text()).toBe(
      'Laden Sie die Seite neu. Der Rechner war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    )
  })
})

describe('a station tablet the laptop turned away', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says that no festival is running instead of blaming the connection', async () => {
    stationLaptop({
      orders: refusal('errors.station.noFestivalIsRunning', { code: 'NoRunningFestival' }),
    })

    const page = await mountPage()

    expect(page.get('[data-test="load-failed"]').text()).toBe('Es ist kein Fest aktiv.')
  })

  it('says the station does not belong to this festival', async () => {
    stationLaptop({
      orders: refusal('errors.station.notPartOfTheFestival', { code: 'StationNotAtTheFestival' }),
    })

    const page = await mountPage()

    expect(page.get('[data-test="load-failed"]').text()).toBe(
      'Diese Ausgabestelle gehört nicht zum laufenden Fest.',
    )
  })
})
