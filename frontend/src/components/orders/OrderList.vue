<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { AppLanguage, OrderSummary } from '../../core/apiTypes'
import OrderRow from './OrderRow.vue'

defineProps<{ orders: OrderSummary[]; language: AppLanguage }>()
defineEmits<{ open: [orderId: string] }>()

const { t } = useI18n()
</script>

<template>
  <div class="order-list">
    <p v-if="orders.length === 0" class="empty">{{ t('orders.empty') }}</p>
    <OrderRow
      v-for="order in orders"
      :key="order.orderId"
      :order="order"
      :language="language"
      @open="$emit('open', order.orderId)"
    />
  </div>
</template>
