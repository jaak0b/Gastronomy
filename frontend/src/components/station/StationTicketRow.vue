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
  <article class="station-ticket-row" :class="{ 'is-pending': isPending }">
    <h2 class="headline">{{ headline }}</h2>
    <span v-if="isReprint" class="reprint-chip">{{ t('station.reprint') }}</span>
    <p class="row-time">{{ t('station.rowTime', { time: ticket.orderCreatedAtUtc }) }}</p>
    <p class="status">{{ t(statusKey) }}</p>
    <p v-for="(line, index) in ticket.lines" :key="index" class="line">
      {{ t('station.line', { quantity: line.quantity, item: line.itemName }) }}
      <span v-if="line.lineNote !== null && line.lineNote !== undefined" class="line-note">
        {{ t('station.lineNote', { note: line.lineNote }) }}
      </span>
    </p>
    <p v-if="ticket.orderNote !== null" class="order-note">
      {{ t('station.orderNote', { note: ticket.orderNote }) }}
    </p>
    <template v-if="isPending">
      <p class="taken-pending">{{ t('station.takenPending', { seconds: secondsLeft }) }}</p>
      <button type="button" class="undo" @click="emit('undo')">{{ t('station.undo') }}</button>
    </template>
    <template v-else-if="ticket.canAcknowledge">
      <button type="button" class="take" @click="emit('take')">{{ t('station.take') }}</button>
      <p class="take-help">{{ t('station.takeHelp') }}</p>
    </template>
    <p v-else class="take-unavailable">
      {{ t(ticket.canAcknowledgeReasonKey ?? 'station.takeUnavailable') }}
    </p>
    <p v-if="noticeKey !== null && noticeKey !== undefined" class="notice">{{ t(noticeKey) }}</p>
  </article>
</template>
