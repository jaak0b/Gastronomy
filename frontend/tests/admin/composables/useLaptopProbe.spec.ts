import { afterEach, describe, expect, it, vi } from 'vitest'
import { useLaptopProbe } from '../../../src/admin/composables/useLaptopProbe'

describe('asking whether this device is the laptop', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says no when the laptop answers the festivals with not found', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 404 })))

    expect(await useLaptopProbe().isThisTheLaptop()).toBe(false)
  })

  it('says yes when the laptop answers the festivals', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ festivals: [] }), { status: 200 })))

    expect(await useLaptopProbe().isThisTheLaptop()).toBe(true)
  })

  it('says yes when the laptop fails for another reason, because only a missing page means another device', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 500 })))

    expect(await useLaptopProbe().isThisTheLaptop()).toBe(true)
  })
})
