import { describe, expect, it } from 'vitest'
import { countedName } from '../../src/core/countedName'

const asked = (key: string, values: { count: number; item: string }): string =>
  `${key} count=${values.count} item=${values.item}`

describe('countedName', () => {
  it('asks for the wording that carries the count in front of the name', () => {
    expect(countedName(2, 'Semmel', asked)).toBe('review.line count=2 item=Semmel')
  })

  it('asks for the same wording for a single portion', () => {
    expect(countedName(1, 'Semmel', asked)).toBe('review.line count=1 item=Semmel')
  })

  it('writes the name on its own when nothing is on the order', () => {
    expect(countedName(0, 'Hauptspeise', asked)).toBe('Hauptspeise')
  })
})
