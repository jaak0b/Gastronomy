<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationTicketRow } from '../../core/apiTypes'
import { assertNever } from '../../core/assertNever'
import { formatSequenceNumber } from '../../core/messageForTicket'

const props = defineProps<{
  ticket: StationTicketRow
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
  switch (props.ticket.status) {
    case 'Queued':
      return 'station.status.waiting'
    case 'Printing':
      return 'station.status.printing'
    case 'Blocked':
      return 'station.status.cannotPrint'
    case 'Failed':
      return 'station.status.failed'
    case 'Unknown':
      return 'station.status.unknown'
    case 'Printed':
    case 'PrintedOnTestPrinter':
      return 'station.alreadyPrinted'
    case 'HandledOnPaper':
      return 'station.takenNote'
    default:
      return assertNever(props.ticket.status)
  }
})

const headline = computed(() =>
  t('station.row', {
    sequence: formatSequenceNumber(props.ticket.sequenceNumber),
    order: props.ticket.globalOrderNumber,
    table: props.ticket.tableLabel,
  }),
)

const isReprint = computed(() => props.ticket.reprintCount > 0)
</script>

<template>
  <v-card class="station-ticket-row mb-3" :class="{ 'is-pending': isPending }">
    <v-card-item>
      <v-card-title class="headline">
        {{ headline }}
        <v-chip v-if="isReprint" class="reprint-chip ms-2" size="small" color="warning">
          {{ t('station.reprint') }}
        </v-chip>
      </v-card-title>
      <v-card-subtitle class="row-time">
        {{ t('station.rowTime', { time: ticket.orderCreatedAtUtc }) }}
      </v-card-subtitle>
    </v-card-item>
    <v-card-text>
      <p class="status text-medium-emphasis">{{ t(statusKey) }}</p>
      <p v-for="(line, index) in ticket.lines" :key="index" class="line text-h6">
        {{ t('station.line', { quantity: line.quantity, item: line.itemName }) }}
        <span v-if="line.lineNote !== null && line.lineNote !== undefined" class="line-note text-body-2">
          {{ t('station.lineNote', { note: line.lineNote }) }}
        </span>
      </p>
      <p v-if="ticket.orderNote !== null" class="order-note">
        {{ t('station.orderNote', { note: ticket.orderNote }) }}
      </p>
      <p v-if="isPending" class="taken-pending">
        {{ t('station.takenPending', { seconds: secondsLeft }) }}
      </p>
      <p v-else-if="ticket.canAcknowledge" class="take-help text-medium-emphasis">
        {{ t('station.takeHelp') }}
      </p>
      <p v-else class="take-unavailable">
        {{ t(ticket.canAcknowledgeReasonKey ?? 'station.takeUnavailable') }}
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
        v-else-if="ticket.canAcknowledge"
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
