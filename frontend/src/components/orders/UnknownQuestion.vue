<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TicketSummary } from '../../core/apiTypes'
import { formatSequenceNumber } from '../../core/messageForTicket'

const props = defineProps<{ ticket: TicketSummary; noticeKey?: string | null }>()
defineEmits<{ answer: [slipIsOnThePile: boolean] }>()

const { t } = useI18n()

const action = computed(() =>
  t('ticket.unknown.action', {
    station: props.ticket.locationName,
    sequence: formatSequenceNumber(props.ticket.sequenceNumber),
  }),
)

const hasNoPaper = computed(() => props.ticket.printerHasPaper === false)
const notice = computed(() => props.noticeKey ?? null)
</script>

<template>
  <section class="unknown-question">
    <p class="action">{{ action }}</p>
    <p class="reason">{{ t('ticket.unknown.reason') }}</p>
    <p v-if="hasNoPaper" class="paper-hint">{{ t('ticket.unknown.paperHint') }}</p>
    <template v-if="notice === null">
      <button type="button" class="slip-is-there" @click="$emit('answer', true)">
        {{ t('ticket.unknown.yes') }}
      </button>
      <button type="button" class="slip-is-missing" @click="$emit('answer', false)">
        {{ t('ticket.unknown.no') }}
      </button>
    </template>
    <p v-else class="notice">{{ t(notice) }}</p>
  </section>
</template>
