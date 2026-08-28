<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { OrderSummary } from '../../core/apiTypes'
import PrintStatusChip from './PrintStatusChip.vue'
import UnknownQuestion from './UnknownQuestion.vue'

const props = defineProps<{ order: OrderSummary }>()
const emit = defineEmits<{
  answer: [stationOrderId: string, slipIsOnThePile: boolean]
  printAnotherCopy: [stationOrderId: string]
}>()

const { t } = useI18n()
const noticeByTicketId = ref<Record<string, string>>({})

function noticeFor(stationOrderId: string): string | null {
  return noticeByTicketId.value[stationOrderId] ?? null
}

function answer(stationOrderId: string, slipIsOnThePile: boolean): void {
  emit('answer', stationOrderId, slipIsOnThePile)
}

function showNotice(stationOrderId: string, key: string): void {
  noticeByTicketId.value = { ...noticeByTicketId.value, [stationOrderId]: key }
}

defineExpose({ showNotice })
</script>

<template>
  <v-container class="order-detail">
    <h1 class="text-h5 mb-4">
      {{ t('orders.detailTitle', { number: props.order.globalOrderNumber }) }}
    </h1>
    <v-card v-for="stationOrder in order.stationOrders" :key="stationOrder.stationOrderId" class="stationOrder mb-3">
      <v-card-text>
        <PrintStatusChip :station-order="stationOrder" :order-number="order.globalOrderNumber" />
        <UnknownQuestion
          v-if="stationOrder.status === 'Unknown'"
          :station-order="stationOrder"
          :notice-key="noticeFor(stationOrder.stationOrderId)"
          @answer="(slipIsOnThePile) => answer(stationOrder.stationOrderId, slipIsOnThePile)"
        />
      </v-card-text>
      <v-card-actions v-if="stationOrder.status === 'Failed'">
        <v-btn class="print-another-copy" variant="tonal" @click="emit('printAnotherCopy', stationOrder.stationOrderId)">
          {{ t('printJob.anotherCopy') }}
        </v-btn>
      </v-card-actions>
    </v-card>
    <p class="changed-mind text-medium-emphasis">{{ t('order.changedMind') }}</p>
  </v-container>
</template>
