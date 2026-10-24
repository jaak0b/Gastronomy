import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { answer, refusal } from '../../support/laptop'
import { FESTIVAL_ID, PLAN_PATH, planLaptop, planOfTheFestival } from './tablePlanFixture'

const AREAS_PATH = '/api/admin/festivals/fest-1/areas'

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('creating an area', () => {
  it('sends the typed name and reads the plan again', async () => {
    const laptop = planLaptop(answer({ areaId: 'area-garten' }, 201))
    const plan = await planOfTheFestival(laptop)

    await plan.createArea(FESTIVAL_ID, { name: 'Garten' })

    expect(laptop.calls).toEqual([
      { url: AREAS_PATH, method: 'POST', body: { name: 'Garten' } },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })

  it('hands back the id of the new area so the editor can open it', async () => {
    const laptop = planLaptop(answer({ areaId: 'area-garten' }, 201))
    const plan = await planOfTheFestival(laptop)

    const created = await plan.createArea(FESTIVAL_ID, { name: 'Garten' })

    expect(created).toEqual({ kind: 'ok', value: { areaId: 'area-garten' } })
  })

  it('hands back the reason when the name is taken', async () => {
    const laptop = planLaptop(refusal('errors.admin.festivals.areaNameTaken'))
    const plan = await planOfTheFestival(laptop)

    const created = await plan.createArea(FESTIVAL_ID, { name: 'halle' })

    expect(created).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.festivals.areaNameTaken', parameters: {}, count: null },
    })
  })
})

describe('changing an area', () => {
  it('sends the new name and reads the plan again', async () => {
    const laptop = planLaptop(answer({ areaId: 'area-halle' }))
    const plan = await planOfTheFestival(laptop)

    await plan.saveArea(FESTIVAL_ID, 'area-halle', { name: 'Große Halle' })

    expect(laptop.calls).toEqual([
      { url: `${AREAS_PATH}/area-halle`, method: 'PUT', body: { name: 'Große Halle' } },
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

})
