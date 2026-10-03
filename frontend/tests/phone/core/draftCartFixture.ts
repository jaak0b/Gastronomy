import type { DraftLine } from '../../../src/phone/core/draftCart'
import { PlaceOrderRequest } from '../../../src/shared/api/generatedSchemas'

export function bratwurstLine(): DraftLine {
  return {
    catalogItemId: 'item-1',
    note: null,
    stationId: null,
    name: 'Bratwurst',
    stationName: '',
  }
}

export function anAttempt(): PlaceOrderRequest {
  return {
    clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
    tableName: 'Tisch 5',
    items: [
      {
        catalogItemId: 'item-1',
        unitPriceCents: 350,
        note: null,
        stationId: null,
      },
    ],
    deliveryModes: [],
  }
}
