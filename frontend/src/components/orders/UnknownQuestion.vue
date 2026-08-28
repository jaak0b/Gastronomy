<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationOrderSummary } from '../../core/apiTypes'
import { formatSequenceNumber } from '../../core/messageForPrintJob'

const props = defineProps<{ stationOrder: StationOrderSummary; noticeKey?: string | null }>()
defineEmits<{ answer: [slipIsOnThePile: boolean] }>()

const { t } = useI18n()

const action = computed(() =>
  t('printJob.unknown.action', {
    station: props.stationOrder.stationName,
    sequence: formatSequenceNumber(props.stationOrder.stationOrderNumber),
  }),
)

const hasNoPaper = computed(() => props.stationOrder.printerHasPaper === false)
const notice = computed(() => props.noticeKey ?? null)
</script>

<template>
  <v-sheet class="unknown-question pa-4 my-2" border rounded>
    <p class="action text-body-1">{{ action }}</p>
    <p class="reason text-medium-emphasis">{{ t('printJob.unknown.reason') }}</p>
    <p v-if="hasNoPaper" class="paper-hint text-medium-emphasis">
      {{ t('printJob.unknown.paperHint') }}
    </p>
    <template v-if="notice === null">
      <v-btn class="slip-is-there mt-2 me-2" color="primary" @click="$emit('answer', true)">
        {{ t('printJob.unknown.yes') }}
      </v-btn>
      <v-btn class="slip-is-missing mt-2" variant="tonal" @click="$emit('answer', false)">
        {{ t('printJob.unknown.no') }}
      </v-btn>
    </template>
    <v-alert v-else class="notice mt-2" type="info" variant="tonal">{{ t(notice) }}</v-alert>
  </v-sheet>
</template>
