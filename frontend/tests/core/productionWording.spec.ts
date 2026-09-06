import { describe, expect, it } from 'vitest'
import {
  advanceItemKey,
  advanceSliceKey,
  deliveryModeKey,
  openItemDeliveryKey,
  openItemProductionKey,
  productionStatusKey,
} from '../../src/core/productionWording'

describe('the words a station tablet uses', () => {
  it('names each production status', () => {
    expect(productionStatusKey('waiting')).toBe('station.status.waiting')
    expect(productionStatusKey('inProduction')).toBe('station.status.inProduction')
    expect(productionStatusKey('finished')).toBe('station.status.finished')
  })

  it('names each delivery mode', () => {
    expect(deliveryModeKey('together')).toBe('station.deliveryTogether')
    expect(deliveryModeKey('asItComes')).toBe('station.deliveryAsItComes')
  })

  it('names the control that moves one item on', () => {
    expect(advanceItemKey('inProduction')).toBe('station.start')
    expect(advanceItemKey('finished')).toBe('station.finish')
  })

  it('names the control that moves a whole order on', () => {
    expect(advanceSliceKey('inProduction')).toBe('station.startAll')
    expect(advanceSliceKey('finished')).toBe('station.finishAll')
  })
})

describe('the words the open items screen uses', () => {
  it('names where an item stands', () => {
    expect(openItemProductionKey('waiting')).toBe('openItems.production.waiting')
    expect(openItemProductionKey('inProduction')).toBe('openItems.production.inProduction')
    expect(openItemProductionKey('finished')).toBe('openItems.production.finished')
  })

  it('names how an item comes out', () => {
    expect(openItemDeliveryKey('together')).toBe('openItems.delivery.together')
    expect(openItemDeliveryKey('asItComes')).toBe('openItems.delivery.asItComes')
  })
})
