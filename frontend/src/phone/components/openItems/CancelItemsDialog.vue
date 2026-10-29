<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TableOrderRecordItemView } from '../../../shared/api/generatedSchemas'
import { useBackStep } from '../../../shared/composables/useBackStep'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { formatPrice } from '../../../shared/core/money'
import type { SettleNotice } from '../../core/openItems'
import BaseFullPageDialog from '../BaseFullPageDialog.vue'
import SettleNoticeAlert from './SettleNotice.vue'

const props = defineProps<{
  items: TableOrderRecordItemView[]
  cashToReturnCents: number
  isCancelling: boolean
  notice: SettleNotice | null
  language: AppLanguage
}>()
const emit = defineEmits<{ confirm: []; reload: []; back: [] }>()

const { t } = useI18n()

useBackStep(() => emit('back'), { refusesBackWhile: () => props.isCancelling })

const cashToReturn = computed(() => formatPrice(props.cashToReturnCents, props.language))

function priceTextFor(cents: number): string {
  return formatPrice(cents, props.language)
}
</script>

<template>
  <BaseFullPageDialog
    class="cancel-items-dialog"
    data-test="cancel-items-dialog"
    :title="t('phone.openItems.labels.cancelTitle')"
  >
    <v-list class="items-to-cancel">
      <v-list-item
        v-for="item in items"
        :key="item.orderItemId"
        class="item-to-cancel"
        data-test="item-to-cancel"
        :data-test-id="item.orderItemId"
      >
        <v-list-item-title class="item-name" data-test="item-name">{{ item.itemName }}</v-list-item-title>
        <v-list-item-subtitle v-if="item.note !== null" class="item-note" data-test="item-note">
          {{ t('common.labels.note', { note: item.note }) }}
        </v-list-item-subtitle>
        <template #append>
          <span
            v-if="item.settledAtUtc !== null"
            class="item-paid app-text"
            data-test="item-paid"
          >
            {{ t('phone.openItems.labels.paid') }}
          </span>
          <span class="item-price app-text" data-test="item-price">
            {{ priceTextFor(item.unitPriceCents) }}
          </span>
        </template>
      </v-list-item>
    </v-list>
    <v-alert
      v-if="cashToReturnCents > 0"
      class="cash-to-return mt-2"
      data-test="cash-to-return"
      type="warning"
      variant="tonal"
    >
      {{ t('phone.openItems.messages.cashToReturn', { amount: cashToReturn }) }}
    </v-alert>
    <SettleNoticeAlert v-if="notice !== null" class="mt-2" :notice="notice" :closable="false" />
    <template #actions>
      <v-btn
        class="confirm-cancel"
        data-test="confirm-cancel"
        color="primary"
        :disabled="isCancelling || items.length === 0"
        @click="emit('confirm')"
      >
        {{ t('phone.openItems.actions.cancelSelected') }}
      </v-btn>
      <v-btn
        v-if="notice !== null"
        class="reload-after-refusal"
        data-test="reload-after-refusal"
        variant="outlined"
        :disabled="isCancelling"
        @click="emit('reload')"
      >
        {{ t('phone.openItems.actions.reload') }}
      </v-btn>
      <v-btn
        class="back"
        data-test="back"
        variant="outlined"
        :disabled="isCancelling"
        @click="emit('back')"
      >
        {{ t('common.actions.back') }}
      </v-btn>
    </template>
  </BaseFullPageDialog>
</template>

<style scoped>
.items-to-cancel {
  background: transparent;
  padding: 0;
}

.item-name,
.item-note {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  overflow-wrap: anywhere;
}

.item-price,
.item-paid {
  flex: 0 0 auto;
  white-space: nowrap;
  padding-inline-start: 0.75rem;
}
</style>
