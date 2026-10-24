import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { answer, emptyAnswer, inTurn, refusal } from '../../support/laptop'
import { FESTIVAL_ID, PLAN_PATH, planLaptop, planOfTheFestival } from './tablePlanFixture'
import { anAdminArea } from '../../support/wireViews'

const AREAS_PATH = '/api/admin/festivals/fest-1/areas'

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('creating an area', () => {
  it('sends the typed name with its place in the order and reads the plan again', async () => {
    const laptop = planLaptop(answer({ areaId: 'area-garten' }, 201))
    const plan = await planOfTheFestival(laptop)

    await plan.createArea(FESTIVAL_ID, { name: 'Garten', sortOrder: 3 })

    expect(laptop.calls).toEqual([
      { url: AREAS_PATH, method: 'POST', body: { name: 'Garten', sortOrder: 3 } },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })

  it('hands back the id of the new area so the editor can open it', async () => {
    const laptop = planLaptop(answer({ areaId: 'area-garten' }, 201))
    const plan = await planOfTheFestival(laptop)

    const created = await plan.createArea(FESTIVAL_ID, { name: 'Garten', sortOrder: 3 })

    expect(created).toEqual({ kind: 'ok', value: { areaId: 'area-garten' } })
  })

  it('hands back the reason when the name is taken', async () => {
    const laptop = planLaptop(refusal('errors.admin.festivals.areaNameTaken'))
    const plan = await planOfTheFestival(laptop)

    const created = await plan.createArea(FESTIVAL_ID, { name: 'halle', sortOrder: 3 })

    expect(created).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.festivals.areaNameTaken', parameters: {}, count: null },
    })
  })
})

describe('changing an area', () => {
  it('sends a new name with the place it already has', async () => {
    const laptop = planLaptop(answer({ areaId: 'area-halle' }))
    const plan = await planOfTheFestival(laptop)

    await plan.saveArea(FESTIVAL_ID, 'area-halle', { name: 'Große Halle', sortOrder: 1 })

    expect(laptop.calls).toEqual([
      { url: `${AREAS_PATH}/area-halle`, method: 'PUT', body: { name: 'Große Halle', sortOrder: 1 } },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })

  it('hides an area', async () => {
    const laptop = planLaptop(answer({ areaId: 'area-zelt' }))
    const plan = await planOfTheFestival(laptop)

    await plan.hideArea(FESTIVAL_ID, 'area-zelt')

    expect(laptop.writes()).toEqual([{ url: `${AREAS_PATH}/area-zelt/hide`, method: 'POST', body: undefined }])
  })

  it('shows a hidden area again', async () => {
    const laptop = planLaptop(answer({ areaId: 'area-zelt' }))
    const plan = await planOfTheFestival(laptop)

    await plan.showArea(FESTIVAL_ID, 'area-zelt')

    expect(laptop.writes()).toEqual([{ url: `${AREAS_PATH}/area-zelt/show`, method: 'POST', body: undefined }])
  })

  it('deletes an area and reads the plan again', async () => {
    const laptop = planLaptop(emptyAnswer(204))
    const plan = await planOfTheFestival(laptop)

    const deleted = await plan.deleteArea(FESTIVAL_ID, 'area-zelt')

    expect(deleted).toEqual({ kind: 'ok', value: null })
    expect(laptop.calls).toEqual([
      { url: `${AREAS_PATH}/area-zelt`, method: 'DELETE', body: undefined },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })

  it('hands back the reason when the festival runs and the area cannot be deleted', async () => {
    const laptop = planLaptop(refusal('errors.admin.festivals.areaStaysWhileTheFestivalRuns'))
    const plan = await planOfTheFestival(laptop)

    const deleted = await plan.deleteArea(FESTIVAL_ID, 'area-zelt')

    expect(deleted).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.festivals.areaStaysWhileTheFestivalRuns', parameters: {}, count: null },
    })
  })
})

describe('moving an area in the order', () => {
  it('sends the new place of the two areas that swap and reads the plan once', async () => {
    const laptop = planLaptop(answer({ areaId: 'area-zelt' }))
    const plan = await planOfTheFestival(laptop)

    await plan.moveArea(FESTIVAL_ID, 'area-zelt', 'up')

    expect(laptop.calls).toEqual([
      { url: `${AREAS_PATH}/area-zelt`, method: 'PUT', body: { name: 'Zelt', sortOrder: 1 } },
      { url: `${AREAS_PATH}/area-halle`, method: 'PUT', body: { name: 'Halle', sortOrder: 2 } },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })

  it('stops at the first refusal, reads the plan again and hands back the reason', async () => {
    const threeAreas = [
      anAdminArea({ areaId: 'area-halle', name: 'Halle', sortOrder: 1 }),
      anAdminArea({ areaId: 'area-zelt', name: 'Zelt', sortOrder: 2 }),
      anAdminArea({ areaId: 'area-garten', name: 'Garten', sortOrder: 3 }),
    ]
    const laptop = planLaptop(
      inTurn(answer({ areaId: 'area-garten' }), refusal('errors.admin.actionFailed', { status: 400 })),
    ).answers('GET', PLAN_PATH, answer({ areas: threeAreas }))
    const plan = await planOfTheFestival(laptop)

    const moved = await plan.moveArea(FESTIVAL_ID, 'area-garten', 'up')

    expect(moved).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
    expect(laptop.urls()).toEqual([`${AREAS_PATH}/area-garten`, `${AREAS_PATH}/area-zelt`, PLAN_PATH])
  })
})
