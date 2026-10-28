import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AdminOverview from '../../../../src/admin/components/overview/AdminOverview.vue'
import { testPlugins } from '../../../support/plugins'
import { stubLaptop, answer, noConnection, type StubbedLaptop } from '../../../support/laptop'
import { aFestival } from '../../../support/wireViews'
import { useConnectionStore } from '../../../../src/shared/stores/connection'
import { useAdminLoadStateStore } from '../../../../src/admin/stores/adminLoadState'

const SUMMER = aFestival()

function overviewLaptop(festivals: unknown[], rest: Record<string, unknown> = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({ categories: rest.categories ?? [] }))
    .answers('GET', '/api/admin/items', answer({ items: rest.items ?? [] }))
    .answers('GET', '/api/admin/stations', answer({ stations: rest.stations ?? [] }))
    .answers('GET', '/api/admin/festivals', answer({ festivals }))
}


function mountOverview(locale: 'de' | 'en' = 'de') {
  return mount(AdminOverview, { global: { plugins: testPlugins(locale) } })
}

describe('the overview while no festival exists at all', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('asks for a festival and lists nothing else', async () => {
    overviewLaptop([])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.get('[data-test="missing-festival"]').text()).toBe('Legen Sie zuerst ein Fest an.')
    expect(overview.findAll('[data-test="readiness-row"]')).toHaveLength(0)
  })
})

describe('the overview while no festival is running', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('says so and lists nothing else', async () => {
    overviewLaptop([{ ...SUMMER, isRunning: false }])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.get('[data-test="not-running"]').text()).toBe(
      'Zurzeit ist kein Fest aktiv. Sehen Sie unter "Feste" nach.',
    )
    expect(overview.findAll('[data-test="readiness-row"]')).toHaveLength(0)
  })

  it('says it in English too', async () => {
    overviewLaptop([{ ...SUMMER, isRunning: false }])

    const overview = mountOverview('en')
    await flushPromises()

    expect(overview.get('[data-test="not-running"]').text()).toBe('No festival is active right now. Look under "Festivals".')
  })

  it('asks the laptop for nothing that belongs to a festival', async () => {
    const laptop = overviewLaptop([{ ...SUMMER, isRunning: false }])

    mountOverview()
    await flushPromises()

    expect(laptop.urls().some((url) => url.includes('festivalId='))).toBe(false)
  })
})

describe('the overview of the festival that is running', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('names that festival', async () => {
    overviewLaptop([SUMMER])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.get('[data-test="running-festival"]').text()).toBe('Sommerfest')
  })

  it('reads the stations and the items of that festival', async () => {
    const laptop = overviewLaptop([SUMMER])

    mountOverview()
    await flushPromises()

    expect(laptop.urls()).toContain('/api/admin/stations?festivalId=fest-1')
    expect(laptop.urls()).toContain('/api/admin/items?festivalId=fest-1')
  })

  it('asks for a station while that festival has none', async () => {
    overviewLaptop([SUMMER])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('[data-test="readiness-row"]').map((row) => row.text())).toContain(
      'Fügen Sie diesem Fest eine Ausgabestelle hinzu, zum Beispiel Küche und Theke.',
    )
  })

  it('asks for the tablet of a station that has none yet', async () => {
    overviewLaptop([SUMMER], {
      stations: [
        {
          stationId: 'station-kueche',
          name: 'Küche',
          sortOrder: 1,
          isActive: true,
          hasDevice: false,
          isAtFestival: true,
          hasItemsToHandOutAtTheRunningFestival: false,
          strandedItemCountWhenSwitchedOff: null,
          strandedItemCountWhenRemovedFromTheFestival: null,
        },
      ],
    })

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('[data-test="readiness-row"]').map((row) => row.text())).toContain(
      'Richten Sie das Tablet für Küche ein. Ohne Tablet sieht diese Ausgabestelle ihre Bestellungen nicht.',
    )
  })

  it('says nothing about a station whose tablet is already set up', async () => {
    overviewLaptop([SUMMER], {
      stations: [
        {
          stationId: 'station-kueche',
          name: 'Küche',
          sortOrder: 1,
          isActive: true,
          hasDevice: true,
          isAtFestival: true,
          hasItemsToHandOutAtTheRunningFestival: false,
          strandedItemCountWhenSwitchedOff: null,
          strandedItemCountWhenRemovedFromTheFestival: null,
        },
      ],
    })

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('[data-test="readiness-row"]').map((row) => row.text())).not.toContain(
      'Richten Sie das Tablet für Küche ein. Ohne Tablet sieht diese Ausgabestelle ihre Bestellungen nicht.',
    )
  })

  it('asks for a category while the laptop holds none', async () => {
    overviewLaptop([SUMMER])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('[data-test="readiness-row"]').map((row) => row.text())).toContain(
      'Legen Sie eine Kategorie an, zum Beispiel Speisen und Getränke.',
    )
  })

  it('says nothing about categories once one exists', async () => {
    overviewLaptop([SUMMER], {
      categories: [
        {
          categoryId: 'category-drinks',
          name: 'Getränke',
          colourHex: '#C62828',
          sortOrder: 1,
          isActive: true,
        },
      ],
    })

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('[data-test="readiness-row"]').map((row) => row.text())).not.toContain(
      'Legen Sie eine Kategorie an, zum Beispiel Speisen und Getränke.',
    )
  })

  it('shows no QR code, because the only enrolment path is the one on the waiters page', async () => {
    overviewLaptop([SUMMER])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.find('img').exists()).toBe(false)
    expect(overview.find('canvas').exists()).toBe(false)
  })
})

describe('the overview after the laptop has put the admin back into its groups', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('reads the festivals, stations, categories and items of the running festival again', async () => {
    const laptop = overviewLaptop([SUMMER])
    mountOverview()
    await flushPromises()
    laptop.forgetCalls()

    await useConnectionStore().refetchAll()
    await flushPromises()

    expect([...laptop.urls()].sort()).toEqual([
      '/api/admin/categories',
      '/api/admin/festivals',
      '/api/admin/items?festivalId=fest-1',
      '/api/admin/stations?festivalId=fest-1',
    ])
  })
})

describe('the overview once the admin has moved to another screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('reads nothing again when the laptop puts the admin back into its groups', async () => {
    const laptop = overviewLaptop([SUMMER])
    const overview = mountOverview()
    await flushPromises()
    overview.unmount()
    laptop.forgetCalls()

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })

  it('no longer keeps the load failure bar up for its items that failed', async () => {
    overviewLaptop([SUMMER]).answers('GET', '/api/admin/items?festivalId=fest-1', noConnection())
    useConnectionStore().state = 'connected'
    const overview = mountOverview()
    await flushPromises()

    overview.unmount()

    expect(useAdminLoadStateStore().showsLoadFailure).toBe(false)
  })
})
