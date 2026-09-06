import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useEstimatesStore } from '../../src/stores/estimates'
import { useSessionStore } from '../../src/stores/session'

function enrolledPhone() {
  useSessionStore().deviceToken = 'token-here'
}

describe('the waiting times the phone asks the laptop for', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the queue of every station the laptop named', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({ stations: [{ stationId: 'station-kueche', queuedMinutes: 12 }] }),
            { status: 200 },
          ),
      ),
    )
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(estimates.stations).toEqual([{ stationId: 'station-kueche', queuedMinutes: 12 }])
  })

  it('asks the laptop at its own address', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(JSON.stringify({ stations: [] }), { status: 200 })
      }),
    )
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(urls).toEqual(['/api/estimates'])
  })

  it('says the times are missing rather than showing a wrong one', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
    enrolledPhone()
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(estimates.loadFailed).toBe(true)
    expect(estimates.stations).toEqual([])
  })

  it('asks for nothing at all before the phone is set up', async () => {
    const fetched = vi.fn()
    vi.stubGlobal('fetch', fetched)
    const estimates = useEstimatesStore()

    await estimates.load()

    expect(fetched).not.toHaveBeenCalled()
  })
})
