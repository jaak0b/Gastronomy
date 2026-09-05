<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { isPaymentNoticeWritten, type SettleNotice } from '../../core/openItems'
import SettleNoticeAlert from './SettleNotice.vue'

defineProps<{ isSettling: boolean; notice: SettleNotice | null }>()
const emit = defineEmits<{ confirm: [paymentNotice: string]; cancel: [] }>()

const { t } = useI18n()
const reason = ref('')
const reasonIsMissing = ref(false)

function confirm(): void {
  if (!isPaymentNoticeWritten(reason.value)) {
    reasonIsMissing.value = true
    return
  }
  emit('confirm', reason.value.trim())
}

function type(typed: string): void {
  reason.value = typed
  if (isPaymentNoticeWritten(typed)) {
    reasonIsMissing.value = false
  }
}
</script>

<template>
  <v-dialog class="free-of-charge-dialog" model-value persistent max-width="480">
    <v-card>
      <v-card-title class="title">{{ t('openItems.freeOfChargeTitle') }}</v-card-title>
      <v-card-text>
        <p class="help mb-4">{{ t('openItems.freeOfChargeHelp') }}</p>
        <v-text-field
          class="reason-field"
          autofocus
          :label="t('openItems.reason')"
          :placeholder="t('openItems.reasonPlaceholder')"
          persistent-placeholder
          :model-value="reason"
          :error="reasonIsMissing"
          :error-messages="reasonIsMissing ? [t('openItems.reasonMissing')] : []"
          @update:model-value="type($event)"
          @keyup.enter="confirm"
        />
        <SettleNoticeAlert v-if="notice !== null" class="mt-2" :notice="notice" :closable="false" />
      </v-card-text>
      <v-card-actions>
        <v-btn
          class="confirm"
          color="primary"
          variant="flat"
          :disabled="isSettling"
          @click="confirm"
        >
          {{ t('openItems.confirmFreeOfCharge') }}
        </v-btn>
        <v-btn class="cancel" variant="text" :disabled="isSettling" @click="emit('cancel')">
          {{ t('openItems.cancel') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
