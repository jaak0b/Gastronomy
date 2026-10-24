import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { answer, emptyAnswer, refusal } from '../../support/laptop'
import { FESTIVAL_ID, PLAN_PATH, planLaptop, planOfTheFestival } from './tablePlanFixture'
import { aGeometry } from '../../support/wireViews'

const TABLES_PATH = '/api/admin/festivals/fest-1/tables'

const SCHANK_SHAPE_PATH = '/api/admin/festivals/fest-1/stations/station-schank/shape'

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('adding a table', () => {
  it('sends the typed name, its area and where it is drawn, and reads the plan again', async () => {
    const laptop = planLaptop(answer({ tableId: 'table-a2' }, 201))
    const plan = await planOfTheFestival(laptop)

    await plan.createTable(FESTIVAL_ID, { areaId: 'area-halle', name: 'A2', geometry: aGeometry({ y: 80 }) })

    expect(laptop.calls).toEqual([
      {
        url: TABLES_PATH,
        method: 'POST',
        body: { areaId: 'area-halle', name: 'A2', geometry: { x: 20, y: 80, width: 80, height: 40, rotation: 0 } },
      },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })

  it('hands back the id of the new table so the editor can select it', async () => {
    const laptop = planLaptop(answer({ tableId: 'table-a2' }, 201))
    const plan = await planOfTheFestival(laptop)

    const created = await plan.createTable(FESTIVAL_ID, { areaId: 'area-halle', name: 'A2', geometry: aGeometry() })

    expect(created).toEqual({ kind: 'ok', value: { tableId: 'table-a2' } })
  })

  it('hands back the reason when another table already has the name', async () => {
    const laptop = planLaptop(refusal('errors.admin.festivals.tableNameTaken'))
    const plan = await planOfTheFestival(laptop)

    const created = await plan.createTable(FESTIVAL_ID, { areaId: 'area-halle', name: 'a1', geometry: aGeometry() })

    expect(created).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.festivals.tableNameTaken', parameters: {}, count: null },
    })
  })
})

describe('changing a table', () => {
  it('sends its name, its area and its geometry exactly as given', async () => {
    const laptop = planLaptop(answer({ tableId: 'table-a1' }))
    const plan = await planOfTheFestival(laptop)
    const geometry = { x: 33.25, y: 47.5, width: 81.125, height: 39.75, rotation: -12.5 }

    await plan.saveTable(FESTIVAL_ID, 'table-a1', { areaId: 'area-halle', name: 'A1', geometry })

    expect(laptop.calls).toEqual([
      {
        url: `${TABLES_PATH}/table-a1`,
        method: 'PUT',
        body: {
          areaId: 'area-halle',
          name: 'A1',
          geometry: { x: 33.25, y: 47.5, width: 81.125, height: 39.75, rotation: -12.5 },
        },
      },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })

  it('hides a table', async () => {
    const laptop = planLaptop(answer({ tableId: 'table-a1' }))
    const plan = await planOfTheFestival(laptop)

    await plan.hideTable(FESTIVAL_ID, 'table-a1')

    expect(laptop.writes()).toEqual([{ url: `${TABLES_PATH}/table-a1/hide`, method: 'POST', body: undefined }])
  })

  it('shows a hidden table again', async () => {
    const laptop = planLaptop(answer({ tableId: 'table-a1' }))
    const plan = await planOfTheFestival(laptop)

    await plan.showTable(FESTIVAL_ID, 'table-a1')

    expect(laptop.writes()).toEqual([{ url: `${TABLES_PATH}/table-a1/show`, method: 'POST', body: undefined }])
  })

  it('deletes a table and reads the plan again', async () => {
    const laptop = planLaptop(emptyAnswer(204))
    const plan = await planOfTheFestival(laptop)

    const deleted = await plan.deleteTable(FESTIVAL_ID, 'table-a1')

    expect(deleted).toEqual({ kind: 'ok', value: null })
    expect(laptop.calls).toEqual([
      { url: `${TABLES_PATH}/table-a1`, method: 'DELETE', body: undefined },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })

  it('hands back the reason when a table with orders cannot be deleted', async () => {
    const laptop = planLaptop(refusal('errors.admin.festivals.tableWithOrdersStays'))
    const plan = await planOfTheFestival(laptop)

    const deleted = await plan.deleteTable(FESTIVAL_ID, 'table-a1')

    expect(deleted).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.festivals.tableWithOrdersStays', parameters: {}, count: null },
    })
  })
})

describe('a station on the plan', () => {
  it('is put in an area with where it is drawn', async () => {
    const laptop = planLaptop(answer({ stationId: 'station-schank' }))
    const plan = await planOfTheFestival(laptop)

    await plan.placeStation(FESTIVAL_ID, 'station-schank', {
      areaId: 'area-zelt',
      geometry: aGeometry({ x: 10, y: 10, width: 120 }),
    })

    expect(laptop.calls).toEqual([
      {
        url: SCHANK_SHAPE_PATH,
        method: 'PUT',
        body: { areaId: 'area-zelt', geometry: { x: 10, y: 10, width: 120, height: 40, rotation: 0 } },
      },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })

  it('is taken off the plan', async () => {
    const laptop = planLaptop(emptyAnswer(204))
    const plan = await planOfTheFestival(laptop)

    const removed = await plan.takeStationOffThePlan(FESTIVAL_ID, 'station-schank')

    expect(removed).toEqual({ kind: 'ok', value: null })
    expect(laptop.calls).toEqual([
      { url: SCHANK_SHAPE_PATH, method: 'DELETE', body: undefined },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
  })
})
