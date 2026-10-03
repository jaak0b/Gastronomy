import { afterEach, describe, expect, it, vi } from 'vitest'
import { useLaptopProbe } from '../../../src/admin/composables/useLaptopProbe'
import { answer, emptyAnswer, stubLaptop } from '../../support/laptop'

describe('asking whether this device is the laptop', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says no when the laptop answers the festivals with not found', async () => {
    stubLaptop().answersEverythingElse(emptyAnswer(404))

    expect(await useLaptopProbe().isThisTheLaptop()).toBe(false)
  })

  it('says yes when the laptop answers the festivals', async () => {
    stubLaptop().answersEverythingElse(answer({ festivals: [] }))

    expect(await useLaptopProbe().isThisTheLaptop()).toBe(true)
  })

  it('says yes when the laptop fails for another reason, because only a missing page means another device', async () => {
    stubLaptop().answersEverythingElse(emptyAnswer(500))

    expect(await useLaptopProbe().isThisTheLaptop()).toBe(true)
  })
})
