import { describe, expect, it } from 'vitest'
import { createPendingCreatedEntities } from '../../../src/admin/core/pendingCreatedEntities'

interface Entry {
  id: string
  name: string
}

const BRATWURST = { id: 'item-alt', name: 'Bratwurst' }

describe('an entity created while a list read is on the way', () => {
  it('is added to a loaded list that does not carry it yet', () => {
    const pending = createPendingCreatedEntities<Entry>((entry) => entry.id)
    pending.remember({ id: 'item-neu', name: 'Pommes' })

    const merged = pending.mergeInto([BRATWURST])

    expect(merged).toEqual([BRATWURST, { id: 'item-neu', name: 'Pommes' }])
  })

  it('is not added twice when the loaded list already carries it', () => {
    const pending = createPendingCreatedEntities<Entry>((entry) => entry.id)
    const created = { id: 'item-neu', name: 'Pommes' }
    pending.remember(created)

    const merged = pending.mergeInto([BRATWURST, created])

    expect(merged).toEqual([BRATWURST, { id: 'item-neu', name: 'Pommes' }])
  })

  it('is forgotten once a loaded list has carried it', () => {
    const pending = createPendingCreatedEntities<Entry>((entry) => entry.id)
    pending.remember({ id: 'item-neu', name: 'Pommes' })
    pending.mergeInto([{ id: 'item-neu', name: 'Pommes' }])

    const merged = pending.mergeInto([BRATWURST])

    expect(merged).toEqual([BRATWURST])
  })

  it('is kept while no loaded list carries it', () => {
    const pending = createPendingCreatedEntities<Entry>((entry) => entry.id)
    pending.remember({ id: 'item-neu', name: 'Pommes' })
    pending.mergeInto([BRATWURST])

    const merged = pending.mergeInto([BRATWURST])

    expect(merged).toEqual([BRATWURST, { id: 'item-neu', name: 'Pommes' }])
  })

  it('leaves a loaded list on its own when nothing was remembered', () => {
    const pending = createPendingCreatedEntities<Entry>((entry) => entry.id)

    expect(pending.mergeInto([BRATWURST])).toEqual([BRATWURST])
  })

  it('is dropped from memory by clear', () => {
    const pending = createPendingCreatedEntities<Entry>((entry) => entry.id)
    pending.remember({ id: 'item-neu', name: 'Pommes' })
    pending.clear()

    expect(pending.mergeInto([])).toEqual([])
  })
})
