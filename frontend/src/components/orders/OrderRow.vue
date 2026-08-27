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
  <v-card class="order-row mb-2" variant="outlined" @click="$emit('open')">
    <v-card-item>
      <v-card-title class="headline">{{ headline }}</v-card-title>
      <v-card-subtitle>
        <span class="total">{{ formatPrice(order.totalCents, language) }}</span>
        <span class="status ms-2" :class="statusKey">{{ t(statusKey) }}</span>
      </v-card-subtitle>
    </v-card-item>
  </v-card>
</template>
