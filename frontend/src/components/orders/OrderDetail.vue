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
  <section class="order-detail">
    <h1>{{ t('orders.detailTitle', { number: props.order.globalOrderNumber }) }}</h1>
    <div v-for="ticket in order.tickets" :key="ticket.ticketId" class="ticket">
      <TicketChip :ticket="ticket" :order-number="order.globalOrderNumber" />
      <UnknownQuestion
        v-if="ticket.status === 'Unknown'"
        :ticket="ticket"
        :notice-key="noticeFor(ticket.ticketId)"
        @answer="(slipIsOnThePile) => answer(ticket.ticketId, slipIsOnThePile)"
      />
      <button
        v-if="ticket.status === 'Failed'"
        type="button"
        class="reprint"
        @click="emit('reprint', ticket.ticketId)"
      >
        {{ t('ticket.reprint') }}
      </button>
    </div>
    <p class="changed-mind">{{ t('order.changedMind') }}</p>
  </section>
</template>
