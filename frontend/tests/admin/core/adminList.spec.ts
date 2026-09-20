import { afterEach, describe, expect, it, vi } from 'vitest'
import { z } from 'zod'
import { loadAdminList } from '../../../src/admin/core/adminList'
import { createLatestRequestGate, type LatestRequestGate } from '../../../src/shared/core/latestRequestGate'

const SCHEMA = z.object({ items: z.array(z.string()) })

interface Watched {
  shown: string[][]
  loadFailed: boolean[]
}

function watched(): Watched {
  return { shown: [], loadFailed: [] }
}

function listLoad(gate: LatestRequestGate, seen: Watched) {
  return {
    path: '/api/admin/items',
    schema: SCHEMA,
    gate,
    itemsOf: (response: { items: string[] }) => response.items,
    showItems: (items: string[]) => {
      seen.shown.push(items)
    },
    setLoadFailed: (failed: boolean) => {
      seen.loadFailed.push(failed)
    },
  }
}

function laptopLists(items: string[]): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify({ items }), { status: 200 })),
  )
}

function laptopRefuses(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify({}), { status: 500 })),
  )
}

async function nextTurn(): Promise<void> {
  await new Promise((resume) => setTimeout(resume, 0))
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('loading a list for an admin screen', () => {
  it('shows the items the laptop listed', async () => {
    laptopLists(['Bratwurst', 'Bier'])
    const seen = watched()

    await loadAdminList(listLoad(createLatestRequestGate(), seen))

    expect(seen.shown).toEqual([['Bratwurst', 'Bier']])
  })

  it('says the load failed when the laptop refused it, and leaves the list alone', async () => {
    laptopRefuses()
    const seen = watched()

    await loadAdminList(listLoad(createLatestRequestGate(), seen))

    expect(seen.loadFailed).toEqual([false, true])
    expect(seen.shown).toEqual([])
  })

  it('clears an earlier failure while it asks again', async () => {
    laptopLists(['Bier'])
    const seen = watched()

    await loadAdminList(listLoad(createLatestRequestGate(), seen))

    expect(seen.loadFailed).toEqual([false])
  })

  it('leaves a late answer off the screen once a newer load has started', async () => {
    let answerTheRequest: () => void = () => undefined
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        await new Promise<void>((resume) => {
          answerTheRequest = resume
        })
        return new Response(JSON.stringify({ items: ['Bratwurst'] }), { status: 200 })
      }),
    )
    const gate = createLatestRequestGate()
    const seen = watched()

    const late = loadAdminList(listLoad(gate, seen))
    await nextTurn()
    gate.startRequest()
    answerTheRequest()
    await late

    expect(seen.shown).toEqual([])
  })

  it('leaves the failure of a late load off the screen once a newer load has started', async () => {
    let answerTheRequest: () => void = () => undefined
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        await new Promise<void>((resume) => {
          answerTheRequest = resume
        })
        return new Response(JSON.stringify({}), { status: 500 })
      }),
    )
    const gate = createLatestRequestGate()
    const seen = watched()

    const late = loadAdminList(listLoad(gate, seen))
    await nextTurn()
    gate.startRequest()
    answerTheRequest()
    await late

    expect(seen.loadFailed).toEqual([false])
  })
})
