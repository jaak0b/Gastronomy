<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationScreenOrderRow } from '../../core/apiTypes'
import { assertNever } from '../../core/assertNever'
import { formatSequenceNumber } from '../../core/sequenceNumber'

const props = defineProps<{
  stationOrder: StationScreenOrderRow
  isPending: boolean
  takeDelaySeconds: number
  noticeKey?: string | null
}>()
const emit = defineEmits<{ take: []; undo: [] }>()

const { t } = useI18n()
const secondsLeft = ref(props.takeDelaySeconds)
let countdown: ReturnType<typeof setInterval> | null = null

function stopCountdown(): void {
  if (countdown !== null) {
    clearInterval(countdown)
    countdown = null
  }
}

watch(
  () => props.isPending,
  (pending) => {
    stopCountdown()
    secondsLeft.value = props.takeDelaySeconds
    if (!pending) {
      return
    }
    countdown = setInterval(() => {
      secondsLeft.value = secondsLeft.value > 0 ? secondsLeft.value - 1 : 0
    }, 1000)
  },
  { immediate: true },
)

onUnmounted(stopCountdown)

const statusKey = computed(() => {
  switch (props.stationOrder.status) {
    case 'Queued':
      return 'station.status.waiting'
    case 'Sending':
      return 'station.status.printing'
    case 'Blocked':
      return 'station.status.cannotPrint'
    case 'Failed':
      return 'station.status.failed'
    case 'Unknown':
      return 'station.status.unknown'
    case 'Printed':
      return 'station.alreadyPrinted'
    case 'HandledOnPaper':
      return 'station.takenNote'
    default:
      return assertNever(props.stationOrder.status)
  }
})

const headline = computed(() =>
  t('station.row', {
    sequence: formatSequenceNumber(props.stationOrder.stationOrderNumber),
    order: props.stationOrder.globalOrderNumber,
    table: props.stationOrder.tableName,
  }),
)

const isReprint = computed(() => props.stationOrder.copyNumber > 0)
</script>

<template>
  <v-card class="station-stationOrder-row mb-3" :class="{ 'is-pending': isPending }">
    <v-card-item>
      <v-card-title class="headline">
        {{ headline }}
        <v-chip v-if="isReprint" class="reprint-chip ms-2" size="small" color="warning">
          {{ t('station.reprint') }}
        </v-chip>
      </v-card-title>
      <v-card-subtitle class="row-time">
        {{ t('station.rowTime', { time: stationOrder.orderCreatedAtUtc }) }}
      </v-card-subtitle>
    </v-card-item>
    <v-card-text>
      <p class="status text-medium-emphasis">{{ t(statusKey) }}</p>
      <p v-for="(line, index) in stationOrder.items" :key="index" class="line text-h6">
        {{ t('station.line', { quantity: line.quantity, item: line.itemName }) }}
        <span v-if="line.itemNote !== null && line.itemNote !== undefined" class="line-note text-body-2">
          {{ t('station.itemNote', { note: line.itemNote }) }}
        </span>
      </p>
      <p v-if="stationOrder.orderNote !== null" class="order-note">
        {{ t('station.orderNote', { note: stationOrder.orderNote }) }}
      </p>
      <p v-if="isPending" class="taken-pending">
        {{ t('station.takenPending', { seconds: secondsLeft }) }}
      </p>
      <p v-else-if="stationOrder.canHandleOnPaper" class="take-help text-medium-emphasis">
        {{ t('station.takeHelp') }}
      </p>
      <p v-else class="take-unavailable">
        {{ t('station.takeUnavailable') }}
      </p>
      <v-alert
        v-if="noticeKey !== null && noticeKey !== undefined"
        class="notice mt-2"
        type="info"
        variant="tonal"
        density="compact"
      >
        {{ t(noticeKey) }}
      </v-alert>
    </v-card-text>
    <v-card-actions>
      <v-btn v-if="isPending" class="undo" variant="tonal" @click="emit('undo')">
        {{ t('station.undo') }}
      </v-btn>
      <v-btn
        v-else-if="stationOrder.canHandleOnPaper"
        class="take"
        color="primary"
        size="x-large"
        @click="emit('take')"
      >
        {{ t('station.take') }}
      </v-btn>
    </v-card-actions>
  </v-card>
</template>
