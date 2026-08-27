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
  <v-sheet class="unknown-question pa-4 my-2" border rounded>
    <p class="action text-body-1">{{ action }}</p>
    <p class="reason text-medium-emphasis">{{ t('ticket.unknown.reason') }}</p>
    <p v-if="hasNoPaper" class="paper-hint text-medium-emphasis">
      {{ t('ticket.unknown.paperHint') }}
    </p>
    <template v-if="notice === null">
      <v-btn class="slip-is-there mt-2 me-2" color="primary" @click="$emit('answer', true)">
        {{ t('ticket.unknown.yes') }}
      </v-btn>
      <v-btn class="slip-is-missing mt-2" variant="tonal" @click="$emit('answer', false)">
        {{ t('ticket.unknown.no') }}
      </v-btn>
    </template>
    <v-alert v-else class="notice mt-2" type="info" variant="tonal">{{ t(notice) }}</v-alert>
  </v-sheet>
</template>
