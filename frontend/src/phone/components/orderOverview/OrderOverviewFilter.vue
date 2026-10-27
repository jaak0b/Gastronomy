<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { OrderStatus, PaymentStatus } from '../../../shared/api/generatedSchemas'
import type { OrderOverviewFilter } from '../../stores/orderOverview'

const props = defineProps<{ filter: OrderOverviewFilter }>()
const emit = defineEmits<{ change: [filter: OrderOverviewFilter] }>()

const { t } = useI18n()

const orderStatusChoices: { value: OrderStatus | null; test: string; labelKey: string }[] = [
  { value: null, test: 'every-order', labelKey: 'phone.orderOverview.filter.every' },
  { value: 'open', test: 'open', labelKey: 'phone.orderOverview.filter.open' },
  { value: 'partiallyFulfilled', test: 'partially-fulfilled', labelKey: 'phone.orderOverview.filter.partiallyHandedOut' },
  { value: 'fulfilled', test: 'fulfilled', labelKey: 'phone.orderOverview.labels.handedOut' },
]

const paymentStatusChoices: { value: PaymentStatus; test: string; labelKey: string }[] = [
  { value: null, test: 'every-item', labelKey: 'phone.orderOverview.filter.every' },
  { value: 'paid', test: 'paid', labelKey: 'phone.openItems.labels.paid' },
  { value: 'notPaid', test: 'not-paid', labelKey: 'phone.orderOverview.filter.notPaid' },
]

function variantFor(isPicked: boolean): 'flat' | 'outlined' {
  return isPicked ? 'flat' : 'outlined'
}
</script>

<template>
  <div class="order-overview-filter" data-test="order-overview-filter">
    <p class="text-body-2 mb-1">{{ t('phone.orderOverview.filter.ordersHeading') }}</p>
    <div class="d-flex flex-wrap ga-2 mb-3">
      <v-chip
        v-for="choice in orderStatusChoices"
        :key="choice.test"
        data-test="order-status-choice"
        :data-test-id="choice.test"
        size="large"
        :variant="variantFor(props.filter.orderStatus === choice.value)"
        @click="emit('change', { ...props.filter, orderStatus: choice.value })"
      >
        {{ t(choice.labelKey) }}
      </v-chip>
    </div>
    <p class="text-body-2 mb-1">{{ t('phone.orderOverview.filter.itemsHeading') }}</p>
    <div class="d-flex flex-wrap ga-2">
      <v-chip
        v-for="choice in paymentStatusChoices"
        :key="choice.test"
        data-test="payment-status-choice"
        :data-test-id="choice.test"
        size="large"
        :variant="variantFor(props.filter.paymentStatus === choice.value)"
        @click="emit('change', { ...props.filter, paymentStatus: choice.value })"
      >
        {{ t(choice.labelKey) }}
      </v-chip>
    </div>
    <v-checkbox
      data-test="include-cancelled"
      density="comfortable"
      hide-details
      :label="t('phone.orderOverview.filter.includeCancelled')"
      :model-value="props.filter.includeCancelled === true"
      @update:model-value="emit('change', { ...props.filter, includeCancelled: $event === true })"
    />
  </div>
</template>
