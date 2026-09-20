<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../../shared/api/apiTypes'
import type { SettleNotice } from '../../core/openItems'
import { formatPrice } from '../../core/totals'
import { useKeyboardInset } from '../../composables/useKeyboardInset'
import AmountPaidFields from '../AmountPaidFields.vue'
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
const keyboardInset = useKeyboardInset()
const amountFields = ref<InstanceType<typeof AmountPaidFields> | null>(null)

const selectedTotal = computed(() => formatPrice(props.selectedTotalCents, props.language))
const canConfirm = computed(
  () => amountFields.value !== null && amountFields.value.settlement !== null,
)

function confirm(): void {
  const settlement = amountFields.value?.settlement
  if (settlement === null || settlement === undefined) {
    return
  }
  emit('confirm', settlement.amountPaidCents, settlement.paymentNotice)
}
</script>

<template>
  <v-dialog class="amount-paid-dialog" model-value persistent fullscreen>
    <v-card class="card d-flex flex-column" :style="{ paddingBottom: `${keyboardInset}px` }">
      <v-card-title class="title">{{ t('openItems.amountPaidTitle') }}</v-card-title>
      <v-card-text class="body flex-grow-1">
        <p class="selected-total mb-4">
          {{ t('openItems.selected', { amount: selectedTotal }) }}
        </p>
        <AmountPaidFields
          ref="amountFields"
          :total-cents="selectedTotalCents"
          :language="language"
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
