import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { testPlugins } from '../../../support/plugins'
import { fireHubEvent, forgetHubEvents } from '../../../support/hubConnection'
import { stubLaptop, answer, neverAnswers, noConnection, type StubbedLaptop } from '../../../support/laptop'
import { aFestival, anAdminArea, anAdminItem, anAdminStation, anAdminTable } from '../../../support/wireViews'

vi.mock('@microsoft/signalr', async () => (await import('../../../support/hubConnection')).signalrModuleFake())

const { default: AdminOverview } = await import('../../../../src/admin/components/overview/AdminOverview.vue')
const { useConnectionStore } = await import('../../../../src/shared/stores/connection')
const { useAdminLoadStateStore } = await import('../../../../src/admin/stores/adminLoadState')
const { useAdminTablePlanStore } = await import('../../../../src/admin/stores/tablePlan')

const SUMMER = aFestival()

const DRINKS = { categoryId: 'category-drinks', name: 'Getränke', colourHex: '#C62828', sortOrder: 1, isActive: true }

const BEER_FROM_THE_KITCHEN = anAdminItem({
  itemId: 'item-bier',
  name: 'Bier',
  atTheFestival: { priceCents: 400, isAvailable: true, stationIds: ['station-kueche'] },
})

const MISSING_TABLE = 'Legen Sie für dieses Fest einen Tischplan an und zeichnen Sie die Tische ein.'

function overviewLaptop(festivals: unknown[], rest: Record<string, unknown> = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({ categories: rest.categories ?? [] }))
    .answers('GET', '/api/admin/items', answer({ items: rest.items ?? [] }))
    .answers('GET', '/api/admin/stations', answer({ stations: rest.stations ?? [] }))
    .answers('GET', '/api/admin/festivals', answer({ festivals }))
    .answers('GET', /\/table-plan$/, answer({ areas: rest.areas ?? [] }))
}

function setUpApartFromTheTables(areas: unknown[]): StubbedLaptop {
  return overviewLaptop([SUMMER], {
    categories: [DRINKS],
    items: [BEER_FROM_THE_KITCHEN],
    stations: [anAdminStation()],
    areas,
  })
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
      'Kein Fest aktiv',
    )
    expect(overview.findAll('[data-test="readiness-row"]')).toHaveLength(0)
  })

  it('says it in English too', async () => {
    overviewLaptop([{ ...SUMMER, isRunning: false }])

    const overview = mountOverview('en')
    await flushPromises()

    expect(overview.get('[data-test="not-running"]').text()).toBe('No festival active')
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

describe('the overview of the festival that is running while its waiters see no table', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it.each([
    ['no table plan', []],
    ['a table plan whose tables are all hidden', [anAdminArea({ tables: [anAdminTable({ isHidden: true })] })]],
    ['a table only in a hidden table plan', [anAdminArea({ isHidden: true, tables: [anAdminTable()] })]],
  ])('asks for a table plan with its tables, and for nothing else, while the festival has %s', async (_, areas) => {
    setUpApartFromTheTables(areas)

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('[data-test="readiness-row"]').map((row) => row.text())).toEqual([MISSING_TABLE])
  })

  it('asks for it in English too', async () => {
    setUpApartFromTheTables([])

    const overview = mountOverview('en')
    await flushPromises()

    expect(overview.findAll('[data-test="readiness-row"]').map((row) => row.text())).toEqual([
      'Create a table plan for this festival and draw its tables.',
    ])
  })

  it('reads the table plan of that festival', async () => {
    const laptop = setUpApartFromTheTables([])

    mountOverview()
    await flushPromises()

    expect(laptop.urls()).toContain('/api/admin/festivals/fest-1/table-plan')
  })
})

describe('the overview of the festival that is running once its waiters see a table', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('says that everything is set up', async () => {
    setUpApartFromTheTables([anAdminArea({ tables: [anAdminTable()] })])

    const overview = mountOverview()
    await flushPromises()

    expect([overview.findAll('[data-test="readiness-row"]').length, overview.text()]).toEqual([
      0,
      expect.stringContaining('Alles ist eingerichtet. Sie können jetzt die Telefone und die Tablets einrichten.'),
    ])
  })
})

describe('the overview while a table is drawn', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    window.history.replaceState({}, '', '/admin')
  })

  it('drops the step and says that everything is set up once the laptop reports the change', async () => {
    const laptop = setUpApartFromTheTables([])
    const overview = mountOverview()
    await useConnectionStore().connect({})
    await flushPromises()
    laptop.answers('GET', /\/table-plan$/, answer({ areas: [anAdminArea({ tables: [anAdminTable()] })] }))

    fireHubEvent('ConfigurationChanged')
    await flushPromises()

    expect([overview.findAll('[data-test="readiness-row"]').length, overview.text()]).toEqual([
      0,
      expect.stringContaining('Alles ist eingerichtet. Sie können jetzt die Telefone und die Tablets einrichten.'),
    ])
  })
})

describe('the overview opened after the admin worked on the plan of another festival', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('asks for a table until the plan of the running festival has arrived, whatever the other plan holds', async () => {
    const laptop = setUpApartFromTheTables([]).answers(
      'GET',
      '/api/admin/festivals/fest-2/table-plan',
      answer({ areas: [anAdminArea({ tables: [anAdminTable()] })] }),
    )
    await useAdminTablePlanStore().loadForFestival('fest-2')
    laptop
      .answers('GET', '/api/admin/items', neverAnswers())
      .answers('GET', '/api/admin/festivals/fest-1/table-plan', neverAnswers())

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('[data-test="readiness-row"]').map((row) => row.text())).toContain(MISSING_TABLE)
  })

  it('asks for the plan of the running festival without waiting for its articles', async () => {
    const laptop = setUpApartFromTheTables([]).answers('GET', '/api/admin/items', neverAnswers())

    mountOverview()
    await flushPromises()

    expect(laptop.urls()).toContain('/api/admin/festivals/fest-1/table-plan')
  })
})

describe('the overview after the laptop has put the admin back into its groups', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('reads the festivals, stations, categories, items and table plan of the running festival again', async () => {
    const laptop = overviewLaptop([SUMMER])
    mountOverview()
    await flushPromises()
    laptop.forgetCalls()

    await useConnectionStore().refetchAll()
    await flushPromises()

    expect([...laptop.urls()].sort()).toEqual([
      '/api/admin/categories',
      '/api/admin/festivals',
      '/api/admin/festivals/fest-1/table-plan',
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
