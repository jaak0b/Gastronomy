import { describe, expect, it } from 'vitest'
import { createLatestRequestGate } from '../../../src/shared/core/latestRequestGate'

describe('the gate that decides which answer is still wanted', () => {
  it('hands out tokens that increase one by one', () => {
    const gate = createLatestRequestGate()

    expect(gate.startRequest()).toBe(1)
    expect(gate.startRequest()).toBe(2)
    expect(gate.startRequest()).toBe(3)
  })

  it('calls the token of the newest request current', () => {
    const gate = createLatestRequestGate()

    const token = gate.startRequest()

    expect(gate.isNewestRequest(token)).toBe(true)
  })

  it('stops calling a token current once a later request starts', () => {
    const gate = createLatestRequestGate()
    const first = gate.startRequest()

    const second = gate.startRequest()

    expect(gate.isNewestRequest(first)).toBe(false)
    expect(gate.isNewestRequest(second)).toBe(true)
  })

  it('leaves the second request the only current one when the first answers after it', async () => {
    const gate = createLatestRequestGate()
    let answerTheFirst: () => void = () => {}
    const firstInFlight = new Promise<void>((resolve) => {
      answerTheFirst = resolve
    })

    const first = gate.startRequest()
    const second = gate.startRequest()
    answerTheFirst()
    await firstInFlight

    expect(gate.isNewestRequest(first)).toBe(false)
    expect(gate.isNewestRequest(second)).toBe(true)
  })
})
