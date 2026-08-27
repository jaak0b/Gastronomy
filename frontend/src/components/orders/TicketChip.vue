<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TicketSummary } from '../../core/apiTypes'
import { formatSequenceNumber, messageForTicket } from '../../core/messageForTicket'

const props = defineProps<{ ticket: TicketSummary; orderNumber: number }>()

const { t } = useI18n()

const label = computed(() =>
  t('orders.ticket', {
    station: props.ticket.locationName,
    sequence: formatSequenceNumber(props.ticket.sequenceNumber),
  }),
)

const message = computed(() => messageForTicket(props.ticket, props.orderNumber))
</script>

<template>
  <div class="ticket-chip">
    <v-chip class="label" size="small">{{ label }}</v-chip>
    <p v-if="message !== null" class="message mt-1">{{ t(message.key, message.parameters) }}</p>
  </div>
</template>
