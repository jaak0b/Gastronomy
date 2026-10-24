import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { useTablePlanStore } from '../../../src/phone/stores/tablePlan'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { answer, aHold, heldUntil, inTurn, noConnection, stubLaptop } from '../../support/laptop'
import { aPlanArea, aPlanTable } from '../../support/wireViews'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const PLAN_PATH = '/api/table-plan'

const HALL_WITH_TISCH_4 = aPlanArea({ tables: [aPlanTable({ handOutProgress: 'none' })] })

const HALL_WITH_TISCH_4_HANDED_OUT = aPlanArea({ tables: [aPlanTable({ handOutProgress: 'all' })] })

function aWaiterPhone(): void {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  useSessionStore().deviceToken = 'token-here'
}

describe('the table plan a waiter\'s phone reads', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    aWaiterPhone()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('holds the areas of the running festival as the laptop sent them', async () => {
    stubLaptop().answers('GET', PLAN_PATH, answer({ areas: [HALL_WITH_TISCH_4] }))
    const tablePlan = useTablePlanStore()

    await tablePlan.load()

    expect({ areas: tablePlan.areas, hasLoaded: tablePlan.hasLoaded }).toEqual({
      areas: [HALL_WITH_TISCH_4],
      hasLoaded: true,
    })
  })

  it('knows nothing about the plan until the first answer has arrived', () => {
    const tablePlan = useTablePlanStore()

    expect(tablePlan.hasLoaded).toBe(false)
  })

  it('says so when the laptop could not be reached, and keeps the plan it already had', async () => {
    stubLaptop().answers('GET', PLAN_PATH, inTurn(answer({ areas: [HALL_WITH_TISCH_4] }), noConnection()))
    const tablePlan = useTablePlanStore()
    await tablePlan.load()

    await tablePlan.load()

    expect({ areas: tablePlan.areas, loadFailed: tablePlan.loadFailed }).toEqual({
      areas: [HALL_WITH_TISCH_4],
      loadFailed: true,
    })
  })

  it('keeps the newest answer when an older one arrives after it', async () => {
    const hold = aHold()
    stubLaptop().answers(
      'GET',
      PLAN_PATH,
      inTurn(heldUntil(hold.released, answer({ areas: [HALL_WITH_TISCH_4] })), answer({ areas: [HALL_WITH_TISCH_4_HANDED_OUT] })),
    )
    const tablePlan = useTablePlanStore()
    const olderLoad = tablePlan.load()
    await tablePlan.load()

    hold.release()
    await olderLoad

    expect(tablePlan.areas).toEqual([HALL_WITH_TISCH_4_HANDED_OUT])
  })
})

describe('the table plan a phone follows', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    aWaiterPhone()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads the plan once the laptop has put the phone into its groups', async () => {
    const laptop = stubLaptop().answers('GET', PLAN_PATH, answer({ areas: [HALL_WITH_TISCH_4] }))
    useTablePlanStore().listen()

    await useConnectionStore().connect({ deviceToken: 'token-here' })

    await vi.waitFor(() => expect(laptop.urls()).toEqual([PLAN_PATH]))
  })

  it('loads the colours again when something happens to an order', async () => {
    stubLaptop().answers(
      'GET',
      PLAN_PATH,
      inTurn(answer({ areas: [HALL_WITH_TISCH_4] }), answer({ areas: [HALL_WITH_TISCH_4_HANDED_OUT] })),
    )
    const tablePlan = useTablePlanStore()
    tablePlan.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await vi.waitFor(() => expect(tablePlan.areas).toEqual([HALL_WITH_TISCH_4]))

    fireHubEvent('OrdersChanged')

    await vi.waitFor(() => expect(tablePlan.areas).toEqual([HALL_WITH_TISCH_4_HANDED_OUT]))
  })

  it('loads the plan again when the admin changes it', async () => {
    const garden = aPlanArea({ areaId: 'area-garten', name: 'Garten' })
    stubLaptop().answers(
      'GET',
      PLAN_PATH,
      inTurn(answer({ areas: [HALL_WITH_TISCH_4] }), answer({ areas: [HALL_WITH_TISCH_4, garden] })),
    )
    const tablePlan = useTablePlanStore()
    tablePlan.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await vi.waitFor(() => expect(tablePlan.areas).toEqual([HALL_WITH_TISCH_4]))

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(tablePlan.areas).toEqual([HALL_WITH_TISCH_4, garden]))
  })
})
