import { beforeEach, describe, expect, it, vi } from 'vitest'

describe('the route the app boots into, read from the address bar', () => {
  beforeEach(() => {
    vi.resetModules()
  })

  it('boots the admin when the laptop opens the admin address', async () => {
    window.history.replaceState({}, '', '/admin')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'festivals', festivalId: null })
  })

  it('boots the admin section when the laptop opens a deep admin address', async () => {
    window.history.replaceState({}, '', '/admin/staff')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'staff', festivalId: null })
  })

  it('boots the station address when it is opened', async () => {
    window.history.replaceState({}, '', '/stations')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'stations' })
  })

  it('boots the admin even when the address carries a trailing slash', async () => {
    window.history.replaceState({}, '', '/admin/')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'festivals', festivalId: null })
  })

  it('boots the admin even when the address was typed with a capital letter', async () => {
    window.history.replaceState({}, '', '/Admin')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'admin', section: 'festivals', festivalId: null })
  })

  it('boots the enrolment landing when a server scans an invitation', async () => {
    window.history.replaceState({}, '', '/j/abc123')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'enrolQr', code: 'abc123' })
  })

  it('boots a waiter that reloads the ordering screen at home, where the table plan is', async () => {
    window.history.replaceState({}, '', '/catalog')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('boots a waiter that reloads the summary at home, which leads on to a locked order', async () => {
    window.history.replaceState({}, '', '/review')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('boots a waiter that reloads the settle view of a table at home', async () => {
    window.history.replaceState({}, '', '/open-items')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('boots a waiter that reloads the orders of a table at home', async () => {
    window.history.replaceState({}, '', '/order-overview')

    const { currentRoute } = await import('../../../src/shared/router/router')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})
