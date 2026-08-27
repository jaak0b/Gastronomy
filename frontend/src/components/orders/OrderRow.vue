<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage, OrderSummary } from '../../core/apiTypes'
import { presentOrderState } from '../../core/orderStateMachine'
import { assertNever } from '../../core/assertNever'
import { formatPrice } from '../../core/totals'

const props = defineProps<{ order: OrderSummary; language: AppLanguage }>()
defineEmits<{ open: [] }>()

const { t } = useI18n()

const statusKey = computed(() => {
  const state = presentOrderState(props.order)
  switch (state) {
    case 'Printing':
      return 'orders.status.printing'
    case 'Printed':
      return 'orders.status.printed'
    case 'HandledOnPaper':
      return 'orders.status.handledOnPaper'
    case 'NeedsAttention':
      return 'orders.status.attention'
    default:
      return assertNever(state)
  }
})

const headline = computed(() =>
  t('orders.row', { number: props.order.globalOrderNumber, table: props.order.tableLabel }),
)
</script>

<template>
  <button type="button" class="order-row" @click="$emit('open')">
    <span class="headline">{{ headline }}</span>
    <span class="total">{{ formatPrice(order.totalCents, language) }}</span>
    <span class="status" :class="statusKey">{{ t(statusKey) }}</span>
  </button>
</template>
