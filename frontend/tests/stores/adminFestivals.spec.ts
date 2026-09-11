import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminFestivalsStore } from '../../src/stores/admin/festivals'

const SUMMER = {
  festivalId: 'fest-1',
  name: 'Sommerfest',
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-19T02:00:00Z',
  isHidden: false,
  isRunning: true,
  stationCount: 2,
  menuItemCount: 8,
  orderCount: 0,
}

const LAST_YEAR = {
  ...SUMMER,
  festivalId: 'fest-0',
  name: 'Sommerfest 2025',
  isHidden: true,
  isRunning: false,
  orderCount: 412,
}

interface Call {
  url: string
  method: string
  body: unknown
}

function laptopLists(festivals: unknown[]): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      calls.push({
        url,
        method: init?.method ?? 'GET',
        body: init?.body === undefined ? undefined : JSON.parse(String(init.body)),
      })
      return new Response(JSON.stringify({ festivals }), { status: 200 })
    }),
  )
  return calls
}

function laptopRefuses(status: number, body: unknown): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (_url: string, init?: RequestInit) =>
      (init?.method ?? 'GET') === 'GET'
        ? new Response(JSON.stringify({ festivals: [] }), { status: 200 })
        : new Response(JSON.stringify(body), { status }),
    ),
  )
}

describe('the festivals the laptop knows about', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('come back hidden ones and all, and the browser is what leaves the hidden out', async () => {
    laptopLists([SUMMER, LAST_YEAR])
    const festivals = useAdminFestivalsStore()

    await festivals.load()

    expect(festivals.festivals).toHaveLength(2)
    expect(festivals.shownFestivals.map((festival) => festival.festivalId)).toEqual(['fest-1'])
  })

  it('name the one running now', async () => {
    laptopLists([SUMMER, LAST_YEAR])
    const festivals = useAdminFestivalsStore()

    await festivals.load()

    expect(festivals.runningFestival?.festivalId).toBe('fest-1')
  })

  it('say the list could not be loaded when the laptop cannot be reached', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => {
        throw new TypeError('Failed to fetch')
      }),
    )
    const festivals = useAdminFestivalsStore()

    await festivals.load()

    expect(festivals.loadFailed).toBe(true)
  })

})

describe('setting a festival up', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    localStorage.clear()
  })

  it('sends the name and both moments as the admin typed them', async () => {
    const calls = laptopLists([SUMMER])
    const festivals = useAdminFestivalsStore()

    await festivals.create({
      name: 'Sommerfest',
      startsAtUtc: '2026-07-18T10:00:00.000Z',
      endsAtUtc: '2026-07-19T02:00:00.000Z',
    })

    const sent = calls.find((call) => call.method === 'POST')
    expect(sent?.url).toBe('/api/admin/festivals')
    expect(sent?.body).toEqual({
      name: 'Sommerfest',
      startsAtUtc: '2026-07-18T10:00:00.000Z',
      endsAtUtc: '2026-07-19T02:00:00.000Z',
    })
  })

  it('copies one festival into a new one under its own name', async () => {
    const calls = laptopLists([SUMMER])
    const festivals = useAdminFestivalsStore()

    await festivals.copy('fest-1', {
      name: 'Herbstfest',
      startsAtUtc: '2026-10-03T10:00:00.000Z',
      endsAtUtc: '2026-10-04T02:00:00.000Z',
    })

    expect(calls.find((call) => call.method === 'POST')?.url).toBe(
      '/api/admin/festivals/fest-1/copy',
    )
  })

  it('hides and shows one festival on its own address', async () => {
    const calls = laptopLists([SUMMER])
    const festivals = useAdminFestivalsStore()

    await festivals.hide('fest-1')
    await festivals.show('fest-1')

    const posts = calls.filter((call) => call.method === 'POST').map((call) => call.url)
    expect(posts).toEqual(['/api/admin/festivals/fest-1/hide', '/api/admin/festivals/fest-1/show'])
  })

  it('keeps the name of the festival that is in the way', async () => {
    laptopRefuses(409, {
      code: 'FestivalOverlaps',
      messageKey: 'admin.festivalOverlaps',
      parameters: { name: 'Sommerfest' },
      details: null,
    })
    const festivals = useAdminFestivalsStore()

    const wasCreated = await festivals.create({
      name: 'Herbstfest',
      startsAtUtc: '2026-07-18T10:00:00.000Z',
      endsAtUtc: '2026-07-19T02:00:00.000Z',
    })

    expect(wasCreated).toBe(false)
    expect(festivals.errorMessage?.key).toBe('admin.festivalOverlaps')
    expect(festivals.errorMessage?.parameters).toEqual({ name: 'Sommerfest' })
  })

  it('says the action did not work when the laptop named no reason', async () => {
    laptopRefuses(500, {})
    const festivals = useAdminFestivalsStore()

    await festivals.save('fest-1', {
      name: 'Sommerfest',
      startsAtUtc: '2026-07-18T10:00:00.000Z',
      endsAtUtc: '2026-07-19T02:00:00.000Z',
    })

    expect(festivals.errorMessage?.key).toBe('admin.actionFailed')
  })
})
