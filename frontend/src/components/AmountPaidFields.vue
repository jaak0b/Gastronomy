<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage, ConfirmedSettlement } from '../shared/api/apiTypes'
import { canBeTypedIntoAEuroField, formatEuroInput, parseEuroInput } from '../shared/core/money'
import { canTheAmountBeSettled, isPaymentNoticeNeeded } from '../core/openItems'

const props = defineProps<{
  totalCents: number
  language: AppLanguage
}>()

const { t } = useI18n()
const typedAmount = ref<string | null>(null)
const reason = ref('')

const shownAmount = computed(
  () => typedAmount.value ?? formatEuroInput(props.totalCents, props.language),
)
const amountPaidCents = computed(() => parseEuroInput(shownAmount.value))
const reasonIsNeeded = computed(
  () =>
    amountPaidCents.value !== null
    && isPaymentNoticeNeeded(amountPaidCents.value, props.totalCents),
)
const settlement = computed<ConfirmedSettlement | null>(() => {
  const paid = amountPaidCents.value
  if (paid === null || !canTheAmountBeSettled(paid, reason.value, props.totalCents)) {
    return null
  }
  return {
    amountPaidCents: paid,
    paymentNotice: reasonIsNeeded.value ? reason.value.trim() : null,
  }
})

function keepWhatCanStillBecomeAnAmount(typed: string): void {
  if (canBeTypedIntoAEuroField(typed)) {
    typedAmount.value = typed
  }
}

function undoARefusedKeystroke(event: Event): void {
  const field = event.target as HTMLInputElement
  if (canBeTypedIntoAEuroField(field.value)) {
    return
  }
  field.value = shownAmount.value
}

defineExpose({ settlement })
</script>

<template>
  <div class="amount-paid-fields">
    <v-text-field
      class="amount-field"
      inputmode="decimal"
      :label="t('openItems.amountPaidField')"
      :model-value="shownAmount"
      @update:model-value="keepWhatCanStillBecomeAnAmount"
      @input="undoARefusedKeystroke"
    />
    <v-text-field
      v-if="reasonIsNeeded"
      v-model="reason"
      class="reason-field"
      maxlength="200"
      :label="t('openItems.amountPaidReason')"
      :placeholder="t('openItems.reasonPlaceholder')"
      persistent-placeholder
    />
  </div>
</template>

<style scoped>
.amount-field {
  margin-block-end: 1.25rem;
}
</style>
