<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TableOrderRecordItemView } from '../../../shared/api/generatedSchemas'
import { useBackStep } from '../../../shared/composables/useBackStep'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { formatPrice } from '../../../shared/core/money'
import type { SettleNotice } from '../../core/openItems'
import BaseFullPageDialog from '../BaseFullPageDialog.vue'
import StatusMark from '../StatusMark.vue'
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
    <div class="items-to-cancel">
      <div class="items-heading" data-test="items-heading">
        <span />
        <span class="column-heading app-small-text" data-test="cancellation-paid-heading">
          {{ t('phone.openItems.labels.paid') }}
        </span>
        <span />
      </div>
      <div
        v-for="item in items"
        :key="item.orderItemId"
        class="item-to-cancel"
        data-test="item-to-cancel"
        :data-test-id="item.orderItemId"
      >
        <div class="item-text">
          <div class="item-name app-item-name" data-test="item-name">{{ item.itemName }}</div>
          <div v-if="item.note !== null" class="item-note app-small-text text-medium-emphasis" data-test="item-note">
            {{ t('common.labels.note', { note: item.note }) }}
          </div>
        </div>
        <StatusMark
          class="item-paid-mark"
          test-name="cancellation-paid-mark"
          :test-id="item.orderItemId"
          :is-done="item.settledAtUtc !== null"
        />
        <span class="item-price app-text" data-test="item-price">
          {{ priceTextFor(item.unitPriceCents) }}
        </span>
      </div>
    </div>
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
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto;
  column-gap: 0.75rem;
  row-gap: 0.5rem;
  align-items: center;
}

.items-heading,
.item-to-cancel {
  grid-column: 1 / -1;
  display: grid;
  grid-template-columns: subgrid;
  align-items: center;
}

.items-heading {
  padding-block-end: 0.25rem;
  border-bottom: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.item-to-cancel {
  padding-block: 0.25rem;
}

.item-text {
  min-width: 0;
  overflow-wrap: anywhere;
}

.item-name,
.item-note {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  overflow-wrap: anywhere;
}

.column-heading,
.item-paid-mark {
  justify-self: center;
}

.item-price {
  justify-self: end;
  white-space: nowrap;
}
</style>
