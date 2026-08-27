import { beforeEach, describe, expect, it, vi } from 'vitest'

describe('the route the app boots into, read from the address bar', () => {
  beforeEach(() => {
    vi.resetModules()
  })

  it('boots the admin when the laptop opens the admin address', async () => {
    window.history.replaceState({}, '', '/admin')

    const { currentRoute } = await import('../../src/router')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'overview' })
  })

  it('boots the admin section when the laptop opens a deep admin address', async () => {
    window.history.replaceState({}, '', '/admin/printers')

    const { currentRoute } = await import('../../src/router')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'printers' })
  })

  it('boots the station page when a station card is scanned', async () => {
    window.history.replaceState({}, '', '/station/abc123')

    const { currentRoute } = await import('../../src/router')

    expect(currentRoute.value).toEqual({ name: 'station', accessKey: 'abc123' })
  })

  it('boots the admin even when the address carries a trailing slash', async () => {
    window.history.replaceState({}, '', '/admin/')

    const { currentRoute } = await import('../../src/router')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'overview' })
  })

  it('boots the admin even when the address was typed with a capital letter', async () => {
    window.history.replaceState({}, '', '/Admin')

    const { currentRoute } = await import('../../src/router')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'overview' })
  })

  it('boots the enrolment landing when a server scans an invitation', async () => {
    window.history.replaceState({}, '', '/j/abc123')

    const { currentRoute } = await import('../../src/router')

    expect(currentRoute.value).toEqual({ name: 'enrolQr', code: 'abc123' })
  })
})
