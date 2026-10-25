import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { answer, refusal } from '../../support/laptop'
import { FESTIVAL_ID, HALL, PLAN_PATH, TABLE_A1, planLaptop, planOfTheFestival } from './tablePlanFixture'

const COPY_PATH = '/api/admin/festivals/fest-1/tables/copy'

const OTHER_FESTIVAL_PLAN_PATH = '/api/admin/festivals/fest-2/table-plan'

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('copying tables', () => {
  it('keeps the source area and a geometry snapshot in memory, never in local storage', async () => {
    const laptop = planLaptop()
    const plan = await planOfTheFestival(laptop)

    plan.copyTables(FESTIVAL_ID, HALL.areaId, [TABLE_A1])

    expect([plan.hasCopiedTables, localStorage.length]).toEqual([true, 0])
  })

  it('does nothing when no table is copied', async () => {
    const laptop = planLaptop()
    const plan = await planOfTheFestival(laptop)

    plan.copyTables(FESTIVAL_ID, HALL.areaId, [])

    expect(plan.hasCopiedTables).toBe(false)
  })
})

describe('pasting into the same area', () => {
  it('sends every chosen table one grid step right and down and reads the plan again', async () => {
    const laptop = planLaptop(answer({ tableIds: ['table-a2'] }, 201))
    const plan = await planOfTheFestival(laptop)
    plan.copyTables(FESTIVAL_ID, HALL.areaId, [TABLE_A1])

    const pasted = await plan.pasteInto(FESTIVAL_ID, HALL.areaId)

    expect(laptop.calls).toEqual([
      {
        url: COPY_PATH,
        method: 'POST',
        body: {
          areaId: 'area-halle',
          tables: [
            {
              sourceTableId: 'table-a1',
              geometry: { x: 30, y: 40, width: 80, height: 40, rotation: 0 },
            },
          ],
        },
      },
      { url: PLAN_PATH, method: 'GET', body: undefined },
    ])
    expect(pasted).toEqual({ kind: 'ok', value: { tableIds: ['table-a2'] } })
  })
})

describe('pasting into another area of the festival', () => {
  it('keeps the coordinates the originals have', async () => {
    const laptop = planLaptop(answer({ tableIds: ['table-a2'] }, 201))
    const plan = await planOfTheFestival(laptop)
    plan.copyTables(FESTIVAL_ID, HALL.areaId, [TABLE_A1])

    await plan.pasteInto(FESTIVAL_ID, 'area-zelt')

    expect(laptop.writtenBodies()).toEqual([
      {
        areaId: 'area-zelt',
        tables: [
          {
            sourceTableId: 'table-a1',
            geometry: { x: 20, y: 30, width: 80, height: 40, rotation: 0 },
          },
        ],
      },
    ])
  })

  it('keeps the clipboard while the same festival is loaded again', async () => {
    const laptop = planLaptop(answer({ tableIds: ['table-a2'] }, 201))
    const plan = await planOfTheFestival(laptop)
    plan.copyTables(FESTIVAL_ID, HALL.areaId, [TABLE_A1])

    await plan.loadForFestival(FESTIVAL_ID)

    expect(plan.hasCopiedTables).toBe(true)
  })
})

describe('the clipboard of another festival', () => {
  it('is dropped as soon as that festival loads', async () => {
    const laptop = planLaptop()
      .answers('GET', OTHER_FESTIVAL_PLAN_PATH, answer({ areas: [] }))
    const plan = await planOfTheFestival(laptop)
    plan.copyTables(FESTIVAL_ID, HALL.areaId, [TABLE_A1])

    await plan.loadForFestival('fest-2')

    expect(plan.hasCopiedTables).toBe(false)
  })

  it('cannot be pasted and sends nothing', async () => {
    const laptop = planLaptop()
      .answers('GET', OTHER_FESTIVAL_PLAN_PATH, answer({ areas: [] }))
    const plan = await planOfTheFestival(laptop)
    plan.copyTables(FESTIVAL_ID, HALL.areaId, [TABLE_A1])
    await plan.loadForFestival('fest-2')
    laptop.forgetCalls()

    const pasted = await plan.pasteInto('fest-2', 'area-zelt')

    expect(pasted).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.actionFailed', parameters: {}, count: null },
    })
    expect(laptop.calls).toEqual([])
  })
})

describe('the menu placement', () => {
  it('puts the top left of the copied group at the clicked drawing point', async () => {
    const laptop = planLaptop(answer({ tableIds: ['table-a2'] }, 201))
    const plan = await planOfTheFestival(laptop)
    plan.copyTables(FESTIVAL_ID, HALL.areaId, [TABLE_A1])

    await plan.pasteInto(FESTIVAL_ID, HALL.areaId, { x: 100, y: 200 })

    expect(laptop.writtenBodies()).toEqual([
      {
        areaId: 'area-halle',
        tables: [
          {
            sourceTableId: 'table-a1',
            geometry: { x: 100, y: 200, width: 80, height: 40, rotation: 0 },
          },
        ],
      },
    ])
  })
})

describe('a paste the laptop refuses', () => {
  it('hands back the reason and does not read the plan again', async () => {
    const laptop = planLaptop(refusal('errors.admin.festivals.tableCopyNameTooLong'))
    const plan = await planOfTheFestival(laptop)
    plan.copyTables(FESTIVAL_ID, HALL.areaId, [TABLE_A1])
    laptop.forgetCalls()

    const pasted = await plan.pasteInto(FESTIVAL_ID, HALL.areaId)

    expect(pasted).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.festivals.tableCopyNameTooLong', parameters: {}, count: null },
    })
    expect(laptop.calls).toEqual([
      {
        url: COPY_PATH,
        method: 'POST',
        body: {
          areaId: 'area-halle',
          tables: [
            {
              sourceTableId: 'table-a1',
              geometry: { x: 30, y: 40, width: 80, height: 40, rotation: 0 },
            },
          ],
        },
      },
    ])
  })
})
