import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminUnitsStore } from '../../../src/admin/stores/units'
import { aHold, answer, heldUntil, inTurn, stubLaptop, type StubbedLaptop } from '../../support/laptop'
import { anAdminUnit } from '../../support/wireViews'

const GRAM = anAdminUnit({ unitId: 'unit-gram', name: 'g' })

const PIECE = anAdminUnit()

const KISTE = anAdminUnit({ unitId: 'unit-kiste', name: 'Kiste' })

interface UnitsScenario {
  listed?: unknown[]
  writeStatus?: number
  writeAnswer?: unknown
}

function unitsLaptop(scenario: UnitsScenario = {}): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer(scenario.writeAnswer ?? {}, scenario.writeStatus ?? 200))
    .answers('GET', /./, answer({ units: scenario.listed ?? [GRAM] }))
}

beforeEach(() => {
  setActivePinia(createPinia())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the unit list of the admin', () => {
  it('holds what the laptop lists', async () => {
    unitsLaptop({ listed: [PIECE, GRAM] })
    const units = useAdminUnitsStore()

    await units.load()

    expect(units.units).toEqual([PIECE, GRAM])
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
        answer({ units: [PIECE, GRAM] }),
      ),
    )
    const units = useAdminUnitsStore()

    const firstRead = units.load()
    await units.load()
    theFirstRead.release()
    await firstRead

    expect(units.units).toEqual([PIECE, GRAM])
  })
})

describe('writing a unit', () => {
  it('creates it with the name as given and shows it at once', async () => {
    const laptop = unitsLaptop({ writeStatus: 201, writeAnswer: KISTE })
    const units = useAdminUnitsStore()

    const result = await units.create({ name: 'Kiste' })

    expect(result).toEqual({ kind: 'ok', value: KISTE })
    expect(laptop.calls).toEqual([
      { url: '/api/admin/units', method: 'POST', body: { name: 'Kiste' } },
    ])
    expect(units.units).toEqual([KISTE])
  })

  it('renames it and reads the list again', async () => {
    const laptop = unitsLaptop({ writeAnswer: { unitId: GRAM.unitId } })
    const units = useAdminUnitsStore()

    const result = await units.save(GRAM.unitId, { name: 'Gramm' })

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

  it('deactivates and activates it through their own routes', async () => {
    const laptop = unitsLaptop({ writeAnswer: { unitId: GRAM.unitId } })
    const units = useAdminUnitsStore()

    await units.setActive(GRAM.unitId, false)
    await units.setActive(GRAM.unitId, true)

    expect(laptop.calls.filter((call) => call.method === 'POST').map((call) => call.url)).toEqual([
      '/api/admin/units/unit-gram/deactivate',
      '/api/admin/units/unit-gram/activate',
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

    const result = await units.save(GRAM.unitId, { name: 'g' })

    expect(result).toEqual({
      kind: 'failed',
      message: { key: 'common.errors.actionFailed', parameters: {}, count: null },
    })
  })
})
