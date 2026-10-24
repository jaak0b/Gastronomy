import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { aHold, answer, heldUntil, inTurn, neverAnswers, stubLaptop } from '../../support/laptop'
import { FESTIVAL_ID, HALL, PLAN_PATH, TENT, planLaptop, planOfTheFestival } from './tablePlanFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../src/shared/stores/connection')
const { useAdminTablePlanStore } = await import('../../../src/admin/stores/tablePlan')

beforeEach(() => {
  setActivePinia(createPinia())
  forgetHubEvents()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the table plan of a festival', () => {
  it('holds every area the laptop lists for that festival', async () => {
    const laptop = planLaptop()
    const plan = useAdminTablePlanStore()

    await plan.loadForFestival(FESTIVAL_ID)

    expect(laptop.urls()).toEqual([PLAN_PATH])
    expect(plan.areas).toEqual([HALL, TENT])
  })

  it('says so when the plan cannot be read', async () => {
    stubLaptop().answersEverythingElse(answer({}, 500))
    const plan = useAdminTablePlanStore()

    await plan.loadForFestival(FESTIVAL_ID)

    expect(plan.loadFailed).toBe(true)
  })

  it('drops the areas of another festival as soon as a different festival is opened', async () => {
    const laptop = planLaptop()
    const plan = await planOfTheFestival(laptop)
    stubLaptop().answersEverythingElse(neverAnswers())

    void plan.loadForFestival('fest-2')

    expect(plan.areas).toEqual([])
  })

  it('keeps the answer to an action when an older read arrives later', async () => {
    const theFirstRead = aHold()
    stubLaptop()
      .answersEverythingElse(answer({}))
      .answers(
        'GET',
        PLAN_PATH,
        inTurn(heldUntil(theFirstRead.released, answer({ areas: [TENT] })), answer({ areas: [HALL, TENT] })),
      )
    const plan = useAdminTablePlanStore()

    const olderRead = plan.loadForFestival(FESTIVAL_ID)
    await plan.hideArea(FESTIVAL_ID, 'area-zelt')
    theFirstRead.release()
    await olderRead

    expect(plan.areas).toEqual([HALL, TENT])
  })
})

describe('the table plan while the editor is open', () => {
  it('is read again when the laptop says the configuration changed', async () => {
    const laptop = planLaptop()
    const plan = await planOfTheFestival(laptop)
    plan.listen()
    await useConnectionStore().connect({})
    await vi.waitFor(() => expect(laptop.urls()).toEqual([PLAN_PATH]))
    laptop.forgetCalls()

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(laptop.urls()).toEqual([PLAN_PATH]))
  })

  it('is read again when the connection asks every screen to reload', async () => {
    const laptop = planLaptop()
    const plan = await planOfTheFestival(laptop)
    plan.listen()

    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([PLAN_PATH])
  })

  it('is left alone once the admin has left the screen', async () => {
    const laptop = planLaptop()
    const plan = await planOfTheFestival(laptop)
    const stopListening = plan.listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(laptop.urls()).toEqual([])
  })
})
