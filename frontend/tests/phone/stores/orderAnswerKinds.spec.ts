import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useOrderStore } from '../../../src/phone/stores/order'
import { TISCH_3, theWaiterTakesTheOrderAt } from '../../support/waiterPhone'

vi.mock('../../../src/shared/api/client', () => ({
  request: vi.fn(async () => ({ kind: 'anAnswerNobodyWroteAHandlerFor' })),
}))

describe('an answer of a kind the phone has no handler for', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    theWaiterTakesTheOrderAt(TISCH_3)
  })

  it('is refused loudly instead of leaving the order on its way for the rest of the evening', async () => {
    const order = useOrderStore()

    await expect(order.send('leaveOpen')).rejects.toThrow()
  })
})
