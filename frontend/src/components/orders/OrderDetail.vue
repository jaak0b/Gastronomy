<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { OrderSummary } from '../../core/apiTypes'
import TicketChip from './TicketChip.vue'
import UnknownQuestion from './UnknownQuestion.vue'

const props = defineProps<{ order: OrderSummary }>()
const emit = defineEmits<{
  answer: [ticketId: string, slipIsOnThePile: boolean]
  reprint: [ticketId: string]
}>()

const { t } = useI18n()
const noticeByTicketId = ref<Record<string, string>>({})

function noticeFor(ticketId: string): string | null {
  return noticeByTicketId.value[ticketId] ?? null
}

function answer(ticketId: string, slipIsOnThePile: boolean): void {
  emit('answer', ticketId, slipIsOnThePile)
}

function showNotice(ticketId: string, key: string): void {
  noticeByTicketId.value = { ...noticeByTicketId.value, [ticketId]: key }
}

defineExpose({ showNotice })
</script>

<template>
  <v-container class="order-detail">
    <h1 class="text-h5 mb-4">
      {{ t('orders.detailTitle', { number: props.order.globalOrderNumber }) }}
    </h1>
    <v-card v-for="ticket in order.tickets" :key="ticket.ticketId" class="ticket mb-3">
      <v-card-text>
        <TicketChip :ticket="ticket" :order-number="order.globalOrderNumber" />
        <UnknownQuestion
          v-if="ticket.status === 'Unknown'"
          :ticket="ticket"
          :notice-key="noticeFor(ticket.ticketId)"
          @answer="(slipIsOnThePile) => answer(ticket.ticketId, slipIsOnThePile)"
        />
      </v-card-text>
      <v-card-actions v-if="ticket.status === 'Failed'">
        <v-btn class="reprint" variant="tonal" @click="emit('reprint', ticket.ticketId)">
          {{ t('ticket.reprint') }}
        </v-btn>
      </v-card-actions>
    </v-card>
    <p class="changed-mind text-medium-emphasis">{{ t('order.changedMind') }}</p>
  </v-container>
</template>
