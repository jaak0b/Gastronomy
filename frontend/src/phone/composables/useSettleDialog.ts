import { ref } from 'vue'
import { assertNever } from '../../shared/core/assertNever'
import type { SettleOutcome, SettlementPaymentMethod } from '../core/openItems'
import { useOpenItemsStore } from '../stores/openItems'

export function useSettleDialog() {
  const openItems = useOpenItemsStore()
  const amountPaidIsOpen = ref(false)

  function closeTheAmountAskedForUnlessTheLaptopRefused(outcome: SettleOutcome): void {
    switch (outcome) {
      case 'accepted':
      case 'answerNeverCame':
        amountPaidIsOpen.value = false
        return
      case 'refused':
        return
      default:
        return assertNever(outcome)
    }
  }

  async function settleAtFullPrice(paymentMethod: SettlementPaymentMethod): Promise<void> {
    await openItems.settle(openItems.selectedTotalCents, null, paymentMethod)
  }

  async function settleTheAmountPaid(
    amountPaidCents: number,
    paymentNotice: string | null,
    paymentMethod: SettlementPaymentMethod,
  ): Promise<void> {
    closeTheAmountAskedForUnlessTheLaptopRefused(
      await openItems.settle(amountPaidCents, paymentNotice, paymentMethod),
    )
  }

  return { amountPaidIsOpen, settleAtFullPrice, settleTheAmountPaid }
}
