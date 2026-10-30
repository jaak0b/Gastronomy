import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useOrderStore } from '../../../src/phone/stores/order'
import { currentRoute, navigate } from '../../../src/shared/router/router'
import { allOnScreen } from '../../support/dom'
import { inTurn, answer, noConnection, neverAnswers, stubLaptop } from '../../support/laptop'
import { TISCH_3, theWaiterTakesTheOrderAt } from '../../support/waiterPhone'
import { wasserOnTheMenu } from './reviewFixture'
import { aPlanArea, aPlanStationShape } from '../../support/wireViews'
import { DRAWING, GARDEN, HALL, mountThePlan, planLaptop, theCatalogNamesTheFestivalThatRuns } from './tablePlanFixture'

const SOMMERFEST = { festivalId: 'fest-1', name: 'Sommerfest' }

enableAutoUnmount(afterEach)

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  navigate('/')
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the waiter\'s home screen', () => {
  it('opens straight on the plan when the festival has exactly one area', async () => {
    planLaptop([HALL])

    const view = await mountThePlan()

    expect(view.get(DRAWING).attributes('data-test-id')).toBe('area-halle')
  })

  it('lists one button per area in the order the laptop sent them when there are several', async () => {
    planLaptop([GARDEN, HALL])

    const view = await mountThePlan()

    expect(view.findAll('[data-test="area-button"]').map((button) => button.text())).toEqual(['Garten', 'Halle'])
    expect(view.find(DRAWING).exists()).toBe(false)
  })

  it('draws the plan of the area the waiter taps', async () => {
    planLaptop([GARDEN, HALL])
    const view = await mountThePlan()

    await view.get('[data-test="area-button"][data-test-id="area-halle"]').trigger('click')

    expect(view.get(DRAWING).attributes('data-test-id')).toBe('area-halle')
  })

  it('shows the shared text of an empty list when the festival has no area', async () => {
    planLaptop([])
    theCatalogNamesTheFestivalThatRuns(SOMMERFEST)

    const view = await mountThePlan()

    expect(view.get('[data-test="no-tables"]').text()).toBe('Keine Einträge')
  })

  it('shows the shared text of an empty list in English', async () => {
    planLaptop([])
    theCatalogNamesTheFestivalThatRuns(SOMMERFEST)

    const view = await mountThePlan('en')

    expect(view.get('[data-test="no-tables"]').text()).toBe('No entries')
  })

  it('says that no festival is active when none runs', async () => {
    planLaptop([])
    theCatalogNamesTheFestivalThatRuns(null)

    const view = await mountThePlan()

    expect(view.get('[data-test="no-festival"]').text()).toBe('Zurzeit ist kein Fest aktiv.')
  })

  it('claims nothing about the tables while it is not yet known whether a festival runs', async () => {
    planLaptop([])

    const view = await mountThePlan()

    expect({
      noTables: view.find('[data-test="no-tables"]').exists(),
      noFestival: view.find('[data-test="no-festival"]').exists(),
    }).toEqual({ noTables: false, noFestival: false })
  })

  it('shows the shared text of an empty list in an area that has no table, and still draws its stations', async () => {
    planLaptop([aPlanArea({ areaId: 'area-garten', name: 'Garten', stationShapes: [aPlanStationShape()] })])

    const view = await mountThePlan()

    expect({
      text: view.get('[data-test="no-tables-in-area"]').text(),
      drawn: view.find(DRAWING).exists(),
    }).toEqual({ text: 'Keine Einträge', drawn: true })
  })

  it('claims nothing about the tables before the plan has arrived', async () => {
    planLaptop([HALL], neverAnswers())

    const view = await mountThePlan()

    expect({
      noTables: view.find('[data-test="no-tables"]').exists(),
      areas: view.findAll('[data-test="area-button"]').length,
    }).toEqual({ noTables: false, areas: 0 })
  })

  it('says the plan could not be loaded and loads it again from the reload button', async () => {
    stubLaptop().answers('GET', '/api/table-plan', inTurn(noConnection(), answer({ areas: [HALL] })))
    const view = await mountThePlan()
    expect(view.get('[data-test="table-plan-load-failed"]').text()).toContain(
      'Der Tischplan konnte nicht geladen werden. Laden Sie die Seite neu.',
    )

    await view.get('[data-test="reload-the-plan"]').trigger('click')
    await flushPromises()

    expect(view.find(DRAWING).exists()).toBe(true)
  })
})

describe('the way to the plan while an order waits for an answer from the laptop', () => {
  it('leads to the summary with the retry instead of the plan', async () => {
    planLaptop([HALL])
    stubLaptop().answersEverythingElse(noConnection())
    wasserOnTheMenu()
    theWaiterTakesTheOrderAt(TISCH_3)
    const order = useOrderStore()
    order.addItem({ catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser' })
    await order.send('leaveOpen')
    planLaptop([HALL])

    await mountThePlan()

    expect(currentRoute.value).toEqual({ name: 'review' })
  })

  it('leads to the area list when the order is not locked', async () => {
    planLaptop([GARDEN, HALL])
    wasserOnTheMenu()
    theWaiterTakesTheOrderAt(TISCH_3)
    useOrderStore().addItem({ catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser' })

    await mountThePlan()

    expect({ route: currentRoute.value, areas: allOnScreen('[data-test="area-button"]').length }).toEqual({
      route: { name: 'home' },
      areas: 2,
    })
  })
})
