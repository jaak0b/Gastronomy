<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../core/apiTypes'
import { canBeTypedIntoAEuroField, formatEuroInput, parseEuroInput } from '../../core/money'
import {
  canTheAmountBeSettled,
  isPaymentNoticeNeeded,
  type SettleNotice,
} from '../../core/openItems'
import { formatPrice } from '../../core/totals'
import SettleNoticeAlert from './SettleNotice.vue'

const props = defineProps<{
  isSettling: boolean
  notice: SettleNotice | null
  selectedTotalCents: number
  language: AppLanguage
}>()
const emit = defineEmits<{
  confirm: [amountPaidCents: number, paymentNotice: string | null]
  cancel: []
}>()

const { t } = useI18n()
const typedAmount = ref(formatEuroInput(props.selectedTotalCents, props.language))
const reason = ref('')

const selectedTotal = computed(() => formatPrice(props.selectedTotalCents, props.language))
const amountPaidCents = computed(() => parseEuroInput(typedAmount.value))
const reasonIsNeeded = computed(
  () =>
    amountPaidCents.value !== null
    && isPaymentNoticeNeeded(amountPaidCents.value, props.selectedTotalCents),
)
const canConfirm = computed(() =>
  canTheAmountBeSettled(amountPaidCents.value, reason.value, props.selectedTotalCents),
)

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
  field.value = typedAmount.value
}

function confirm(): void {
  const paid = amountPaidCents.value
  if (paid === null || !canConfirm.value) {
    return
  }
  emit('confirm', paid, reasonIsNeeded.value ? reason.value.trim() : null)
}
</script>

<template>
  <v-dialog class="amount-paid-dialog" model-value persistent fullscreen>
    <v-card class="card d-flex flex-column">
      <v-card-title class="title">{{ t('openItems.amountPaidTitle') }}</v-card-title>
      <v-card-text class="body flex-grow-1">
        <p class="selected-total mb-4">
          {{ t('openItems.selected', { amount: selectedTotal }) }}
        </p>
        <v-text-field
          class="amount-field"
          inputmode="decimal"
          :label="t('openItems.amountPaidField')"
          :model-value="typedAmount"
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
        <SettleNoticeAlert v-if="notice !== null" class="mt-2" :notice="notice" :closable="false" />
      </v-card-text>
      <v-card-actions class="actions flex-column align-stretch">
        <v-btn
          class="confirm"
          color="primary"
          variant="flat"
          size="large"
          :disabled="isSettling || !canConfirm"
          @click="confirm"
        >
          {{ t('openItems.amountPaidConfirm') }}
        </v-btn>
        <v-btn
          class="cancel"
          variant="outlined"
          size="large"
          :disabled="isSettling"
          @click="emit('cancel')"
        >
          {{ t('openItems.amountPaidCancel') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.card {
  height: 100%;
  border-radius: 0;
}

.body {
  font-size: 1.05rem;
  line-height: 1.4;
}

.selected-total {
  font-weight: 600;
}

.amount-field {
  margin-block-end: 1.25rem;
}

.title {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  line-height: 1.35;
  padding-block: 1rem;
}

.actions {
  padding: 0 1rem 1rem;
}

.actions .v-btn {
  width: 100%;
  min-width: 0;
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  text-transform: none;
  letter-spacing: normal;
}

.actions .v-btn :deep(.v-btn__content) {
  white-space: normal;
}
</style>
