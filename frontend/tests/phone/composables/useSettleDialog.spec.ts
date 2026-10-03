import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h } from 'vue'
import { useSettleDialog } from '../../../src/phone/composables/useSettleDialog'
import { useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'

const SETTLED = { settledOrderItemIds: [], reappliedOrderItemIds: [], alreadySettledByOthersOrderItemIds: [] }

function settleAnswers(answer: () => Response | Promise<Response>): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) =>
      url === '/api/open-items/settle'
        ? answer()
        : new Response(JSON.stringify({ tables: [], itemsWithoutAnOrderCount: 0 }), { status: 200 }),
    ),
  )
}

function mountWithTheAmountDialogOpen() {
  let settling: ReturnType<typeof useSettleDialog> | null = null
  mount(
    defineComponent({
      setup() {
        settling = useSettleDialog()
        return () => h('div')
      },
    }),
    { global: { plugins: testPlugins() } },
  )
  if (settling === null) {
    throw new Error('The settling did not set up.')
  }
  const ready = settling as ReturnType<typeof useSettleDialog>
  ready.amountPaidIsOpen.value = true
  return ready
}

describe('the amount dialog after the waiter settled through it', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('closes once the laptop accepted the settlement', async () => {
    settleAnswers(() => new Response(JSON.stringify(SETTLED), { status: 200 }))
    const settling = mountWithTheAmountDialogOpen()

    await settling.settleTheAmountPaid(300, 'Rest geschenkt', 'cash')

    expect(settling.amountPaidIsOpen.value).toBe(false)
  })

  it('stays open when the laptop refused, so what was typed is still there', async () => {
    settleAnswers(
      () =>
        new Response(
          JSON.stringify({
            code: 'ValidationFailed',
            messageKey: 'errors.openItems.settleFailed',
            parameters: {},
            details: null,
          }),
          { status: 400 },
        ),
    )
    const settling = mountWithTheAmountDialogOpen()

    await settling.settleTheAmountPaid(300, 'Rest geschenkt', 'cash')

    expect(settling.amountPaidIsOpen.value).toBe(true)
  })

  it('closes when the answer never came, because the reload is on the list', async () => {
    settleAnswers(() => {
      throw new TypeError('the laptop cannot be reached')
    })
    const settling = mountWithTheAmountDialogOpen()

    await settling.settleTheAmountPaid(300, 'Rest geschenkt', 'cash')

    expect(settling.amountPaidIsOpen.value).toBe(false)
  })
})
