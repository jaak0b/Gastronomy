<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useBackStep } from '../../../shared/composables/useBackStep'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { isAPaymentMethodNeeded, type SettleNotice, type SettlementPaymentMethod } from '../../core/openItems'
import { formatPrice } from '../../../shared/core/money'
import BaseDialogButton from '../../../shared/components/BaseDialogButton.vue'
import AmountPaidFields from '../AmountPaidFields.vue'
import BaseFullPageDialog from '../BaseFullPageDialog.vue'
import SettleNoticeAlert from './SettleNotice.vue'

const props = defineProps<{
  isSettling: boolean
  notice: SettleNotice | null
  selectedTotalCents: number
  language: AppLanguage
}>()
const emit = defineEmits<{
  confirm: [amountPaidCents: number, paymentNotice: string | null, paymentMethod: SettlementPaymentMethod]
  reload: []
  cancel: []
}>()

const { t } = useI18n()
const amountFields = ref<InstanceType<typeof AmountPaidFields> | null>(null)

useBackStep(() => emit('cancel'), { refusesBackWhile: () => props.isSettling })

const selectedTotal = computed(() => formatPrice(props.selectedTotalCents, props.language))
const canConfirm = computed(
  () => amountFields.value !== null && amountFields.value.settlement !== null,
)
const offersCashAndCard = computed(() => {
  const typed = amountFields.value?.amountPaidCents
  return typed === null || typed === undefined || isAPaymentMethodNeeded(typed)
})

function confirm(paymentMethod: SettlementPaymentMethod): void {
  const settlement = amountFields.value?.settlement
  if (settlement === null || settlement === undefined) {
    return
  }
  emit('confirm', settlement.amountPaidCents, settlement.paymentNotice, paymentMethod)
}
</script>

<template>
  <BaseFullPageDialog
    class="amount-paid-dialog"
    data-test="amount-paid-dialog"
    :title="t('phone.openItems.labels.amountPaidTitle')"
  >
    <p class="selected-total mb-4" data-test="selected-total">
      {{ t('phone.openItems.labels.selected', { amount: selectedTotal }) }}
    </p>
    <AmountPaidFields
      ref="amountFields"
      :total-cents="selectedTotalCents"
      :language="language"
    />
    <SettleNoticeAlert v-if="notice !== null" class="mt-2" :notice="notice" :closable="false" />
    <template #actions>
      <template v-if="offersCashAndCard">
        <BaseDialogButton
          class="confirm-in-cash"
          data-test="confirm-in-cash"
          intent="secondary"
          :disabled="isSettling || !canConfirm"
          @click="confirm('cash')"
        >
          {{ t('phone.openItems.actions.settleInCash') }}
        </BaseDialogButton>
        <BaseDialogButton
          class="confirm-by-card"
          data-test="confirm-by-card"
          intent="secondary"
          :disabled="isSettling || !canConfirm"
          @click="confirm('card')"
        >
          {{ t('phone.openItems.actions.settleByCard') }}
        </BaseDialogButton>
      </template>
      <BaseDialogButton
        v-else
        class="confirm-nothing-paid"
        data-test="confirm-nothing-paid"
        intent="primary"
        :disabled="isSettling || !canConfirm"
        @click="confirm('none')"
      >
        {{ t('phone.openItems.actions.settle') }}
      </BaseDialogButton>
      <BaseDialogButton
        v-if="notice !== null"
        class="reload-after-refusal"
        data-test="reload-after-refusal"
        intent="quiet"
        :disabled="isSettling"
        @click="emit('reload')"
      >
        {{ t('phone.openItems.actions.reload') }}
      </BaseDialogButton>
      <BaseDialogButton
        class="cancel"
        data-test="cancel"
        intent="quiet"
        :disabled="isSettling"
        @click="emit('cancel')"
      >
        {{ t('common.actions.cancel') }}
      </BaseDialogButton>
    </template>
  </BaseFullPageDialog>
</template>

<style scoped>
.selected-total {
  font-weight: 600;
}
</style>
