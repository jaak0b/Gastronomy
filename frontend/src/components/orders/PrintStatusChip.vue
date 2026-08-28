<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationOrderSummary } from '../../core/apiTypes'
import { formatSequenceNumber, messageForPrintJob } from '../../core/messageForPrintJob'

const props = defineProps<{ stationOrder: StationOrderSummary; orderNumber: number }>()

const { t } = useI18n()

const label = computed(() =>
  t('orders.stationOrder', {
    station: props.stationOrder.stationName,
    sequence: formatSequenceNumber(props.stationOrder.stationOrderNumber),
  }),
)

const message = computed(() => messageForPrintJob(props.stationOrder, props.orderNumber))
</script>

<template>
  <div class="stationOrder-chip">
    <v-chip class="label" size="small">{{ label }}</v-chip>
    <p v-if="message !== null" class="message mt-1">{{ t(message.key, message.parameters) }}</p>
  </div>
</template>
