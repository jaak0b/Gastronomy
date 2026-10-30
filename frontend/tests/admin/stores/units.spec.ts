import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminUnitsStore } from '../../../src/admin/stores/units'
import { aHold, answer, emptyAnswer, heldUntil, inTurn, stubLaptop, type StubbedLaptop } from '../../support/laptop'
import { GRAM_UNIT, KISTE_UNIT, PIECE_UNIT } from '../../support/wireViews'

interface UnitsScenario {
  listed?: unknown[]
  writeStatus?: number
  writeAnswer?: unknown
}

function unitsLaptop(scenario: UnitsScenario = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer(scenario.writeAnswer ?? {}, scenario.writeStatus ?? 200))
    .answers('GET', /./, answer({ units: scenario.listed ?? [GRAM_UNIT] }))
}

function deletingLaptop(listedAfterwards: unknown[]): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(emptyAnswer(204))
    .answers('GET', '/api/admin/units', answer({ units: listedAfterwards }))
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the unit list of the admin', () => {
  it('holds what the laptop lists', async () => {
    unitsLaptop({ listed: [PIECE_UNIT, GRAM_UNIT] })
    const units = useAdminUnitsStore()

    await units.load()

    expect(units.units).toEqual([PIECE_UNIT, GRAM_UNIT])
    expect(units.loadFailed).toBe(false)
  })

  it('says so when the list cannot be read', async () => {
    stubLaptop().answersEverythingElse(answer({}, 500))
    const units = useAdminUnitsStore()

    await units.load()

    expect(units.loadFailed).toBe(true)
  })

  it('keeps the newer answer when an older read arrives later', async () => {
    const theFirstRead = aHold()
    stubLaptop().answersEverythingElse(
      inTurn(
        heldUntil(theFirstRead.released, answer({ units: [] })),
        answer({ units: [PIECE_UNIT, GRAM_UNIT] }),
      ),
    )
    const units = useAdminUnitsStore()

    const firstRead = units.load()
    await units.load()
    theFirstRead.release()
    await firstRead

    expect(units.units).toEqual([PIECE_UNIT, GRAM_UNIT])
  })
})

describe('writing a unit', () => {
  it('creates it with the name as given and shows it at once', async () => {
    const laptop = unitsLaptop({ writeStatus: 201, writeAnswer: KISTE_UNIT })
    const units = useAdminUnitsStore()

    const result = await units.create({ name: 'Kiste' })

    expect(result).toEqual({ kind: 'ok', value: KISTE_UNIT })
    expect(laptop.calls).toEqual([
      { url: '/api/admin/units', method: 'POST', body: { name: 'Kiste' } },
    ])
    expect(units.units).toEqual([KISTE_UNIT])
  })

  it('renames it and reads the list again', async () => {
    const laptop = unitsLaptop({ writeAnswer: { unitId: GRAM_UNIT.unitId } })
    const units = useAdminUnitsStore()

    const result = await units.save(GRAM_UNIT.unitId, { name: 'Gramm' })

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(laptop.calls).toEqual([
      {
        url: '/api/admin/units/unit-gram',
        method: 'PUT',
        body: { name: 'Gramm' },
      },
      { url: '/api/admin/units', method: 'GET', body: undefined },
    ])
  })

  it('hands back the reason the laptop named when it refuses', async () => {
    unitsLaptop({
      writeStatus: 422,
      writeAnswer: {
        code: 'UnprocessableEntity',
        messageKey: 'errors.admin.units.nameTaken',
        parameters: { name: 'g' },
        details: null,
      },
    })
    const units = useAdminUnitsStore()

    const result = await units.create({ name: 'g' })

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'errors.admin.units.nameTaken', parameters: { name: 'g' }, count: null },
    })
    expect(units.units).toEqual([])
  })

  it('says the action did not work when the laptop named no reason', async () => {
    unitsLaptop({ writeStatus: 500 })
    const units = useAdminUnitsStore()

    const result = await units.save(GRAM_UNIT.unitId, { name: 'g' })

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'common.errors.actionFailed', parameters: {}, count: null },
    })
  })
})

describe('deleting a unit', () => {
  it('deletes it through its own route and reads the list again', async () => {
    const laptop = deletingLaptop([GRAM_UNIT])
    const units = useAdminUnitsStore()

    const result = await units.deleteUnit(KISTE_UNIT.unitId)

    expect(result).toEqual({ kind: 'ok', value: null })
    expect(laptop.calls).toEqual([
      { url: '/api/admin/units/unit-kiste', method: 'DELETE', body: undefined },
      { url: '/api/admin/units', method: 'GET', body: undefined },
    ])
  })

  it('hands back the shared failure when the laptop refuses to delete a unit in use', async () => {
    unitsLaptop({
      writeStatus: 409,
      writeAnswer: {
        code: 'UnitUsedByIngredients',
        messageKey: 'common.errors.actionFailed',
        parameters: {},
        details: null,
      },
    })
    const units = useAdminUnitsStore()

    const result = await units.deleteUnit(GRAM_UNIT.unitId)

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'common.errors.actionFailed', parameters: {}, count: null },
    })
  })
})
