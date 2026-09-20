import { describe, expect, it } from 'vitest'
import { startingUpMessageKey } from '../../src/core/startingUp'

describe('what the starting screen says', () => {
  it('says nothing while the laptop has not answered yet', () => {
    expect(startingUpMessageKey(null)).toBeNull()
  })

  it('says the laptop was not reached when the device could not get to it', () => {
    expect(startingUpMessageKey('theLaptopWasNotReached')).toBe('startingUp.laptopNotReached')
  })

  it('says the device could not start when the laptop answered with nothing usable', () => {
    expect(startingUpMessageKey('theLaptopCouldNotAnswer')).toBe('startingUp.couldNotStart')
  })
})
