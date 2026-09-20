import { describe, expect, it } from 'vitest'
import { createLatestRequestGate } from '../../src/core/latestRequestGate'

describe('the gate that decides which answer is still wanted', () => {
  it('hands out tokens that increase one by one', () => {
    const gate = createLatestRequestGate()

    expect(gate.start()).toBe(1)
    expect(gate.start()).toBe(2)
    expect(gate.start()).toBe(3)
  })

  it('calls the token of the newest request current', () => {
    const gate = createLatestRequestGate()

    const token = gate.start()

    expect(gate.isCurrent(token)).toBe(true)
  })

  it('stops calling a token current once a later request starts', () => {
    const gate = createLatestRequestGate()
    const first = gate.start()

    const second = gate.start()

    expect(gate.isCurrent(first)).toBe(false)
    expect(gate.isCurrent(second)).toBe(true)
  })

  it('leaves the second request the only current one when the first answers after it', async () => {
    const gate = createLatestRequestGate()
    let answerTheFirst: () => void = () => {}
    const firstInFlight = new Promise<void>((resolve) => {
      answerTheFirst = resolve
    })

    const first = gate.start()
    const second = gate.start()
    answerTheFirst()
    await firstInFlight

    expect(gate.isCurrent(first)).toBe(false)
    expect(gate.isCurrent(second)).toBe(true)
  })
})
