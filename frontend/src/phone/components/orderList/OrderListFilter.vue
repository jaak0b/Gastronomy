<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { OrderStatus, PaymentStatus } from '../../../shared/api/generatedSchemas'
import type { TableSearchFilter } from '../../core/openItems'

const props = defineProps<{ filter: TableSearchFilter }>()
const emit = defineEmits<{ change: [filter: TableSearchFilter] }>()

const { t } = useI18n()

const EVERY = 'every'

type OrderStatusChoice = OrderStatus | typeof EVERY
type PaymentStatusChoice = NonNullable<PaymentStatus> | typeof EVERY

function optionOf<T extends string>(value: T, testName: string, labelKey: string) {
  return { value, title: t(labelKey), props: { 'data-test': testName, 'data-test-id': value } }
}

const orderStatusOptions = computed(() => [
  optionOf<OrderStatusChoice>(EVERY, 'order-status-option', 'phone.orderOverview.filter.every'),
  optionOf<OrderStatusChoice>('open', 'order-status-option', 'phone.orderOverview.filter.open'),
  optionOf<OrderStatusChoice>('partiallyFulfilled', 'order-status-option', 'phone.orderOverview.filter.partiallyHandedOut'),
  optionOf<OrderStatusChoice>('fulfilled', 'order-status-option', 'phone.orderOverview.labels.handedOut'),
])

const paymentStatusOptions = computed(() => [
  optionOf<PaymentStatusChoice>(EVERY, 'payment-status-option', 'phone.orderOverview.filter.every'),
  optionOf<PaymentStatusChoice>('paid', 'payment-status-option', 'phone.openItems.labels.paid'),
  optionOf<PaymentStatusChoice>('notPaid', 'payment-status-option', 'phone.orderOverview.filter.notPaid'),
])

function pickOrderStatus(choice: OrderStatusChoice): void {
  emit('change', { ...props.filter, orderStatus: choice === EVERY ? null : choice })
}

function pickPaymentStatus(choice: PaymentStatusChoice): void {
  emit('change', { ...props.filter, paymentStatus: choice === EVERY ? null : choice })
}
</script>

<template>
  <div class="order-list-filter" data-test="order-list-filter">
    <div class="d-flex ga-3">
      <v-select
        class="filter-choice"
        data-test="order-status-filter"
        :label="t('phone.orderOverview.filter.ordersHeading')"
        :items="orderStatusOptions"
        :model-value="filter.orderStatus ?? EVERY"
        variant="outlined"
        density="comfortable"
        hide-details
        @update:model-value="pickOrderStatus"
      />
      <v-select
        class="filter-choice"
        data-test="payment-status-filter"
        :label="t('phone.orderOverview.filter.itemsHeading')"
        :items="paymentStatusOptions"
        :model-value="filter.paymentStatus ?? EVERY"
        variant="outlined"
        density="comfortable"
        hide-details
        @update:model-value="pickPaymentStatus"
      />
    </div>
    <v-checkbox
      data-test="include-cancelled"
      density="comfortable"
      hide-details
      :label="t('phone.orderOverview.filter.includeCancelled')"
      :model-value="filter.includeCancelled === true"
      @update:model-value="emit('change', { ...filter, includeCancelled: $event === true })"
    />
  </div>
</template>

<style scoped>
.filter-choice {
  flex: 1 1 0;
  min-width: 0;
}
</style>
