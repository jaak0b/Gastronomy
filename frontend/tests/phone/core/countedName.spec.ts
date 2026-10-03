import { describe, expect, it } from 'vitest'
import { countedName } from '../../../src/phone/core/countedName'
import type { Translate } from '../../../src/shared/core/translation'

const asked: Translate = (key, values = {}) => `${key} count=${values.count} item=${values.item}`

describe('countedName', () => {
  it('asks for the wording that carries the count in front of the name', () => {
    expect(countedName(2, 'Semmel', asked)).toBe('common.labels.countTimesItem count=2 item=Semmel')
  })

  it('asks for the same wording for a single portion', () => {
    expect(countedName(1, 'Semmel', asked)).toBe('common.labels.countTimesItem count=1 item=Semmel')
  })

  it('writes the name on its own when nothing is on the order', () => {
    expect(countedName(0, 'Hauptspeise', asked)).toBe('Hauptspeise')
  })
})
