<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TableOrderRecordItemView } from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { createLatestRequestGate } from '../../../shared/core/latestRequestGate'
import { formatFestivalMoment } from '../../../shared/core/moments'
import { formatPrice } from '../../../shared/core/money'
import { useSessionStore } from '../../../shared/stores/session'
import { readTheItemHistory, type ItemHistoryAnswer } from '../../api/itemHistory'
import { historyEntriesOf, type ItemHistoryEntry } from '../../core/itemHistory'
import BaseFullPageDialog from '../BaseFullPageDialog.vue'

const props = defineProps<{ item: TableOrderRecordItemView; language: AppLanguage }>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()
const session = useSessionStore()
const loadGate = createLatestRequestGate()

const answer = ref<ItemHistoryAnswer | null>(null)

const entries = computed(() => (answer.value?.kind === 'found' ? historyEntriesOf(answer.value.history) : []))

const failureKey = computed(() => {
  if (answer.value === null) {
    return null
  }
  switch (answer.value.kind) {
    case 'found':
      return null
    case 'refused':
      return answer.value.failureKey
    case 'unanswered':
      return 'phone.openItems.errors.loadFailed'
    default:
      return assertNever(answer.value)
  }
})

async function load(): Promise<void> {
  const token = session.deviceToken
  if (token === null) {
    return
  }
  const gateNumber = loadGate.startRequest()
  answer.value = null
  const read = await readTheItemHistory(props.item.orderItemId, token)
  if (loadGate.isNewestRequest(gateNumber)) {
    answer.value = read
  }
}

const priceText = computed(() =>
  t('phone.itemHistory.labels.price', { price: formatPrice(props.item.unitPriceCents, props.language) }),
)

function momentOf(entry: ItemHistoryEntry): string {
  return formatFestivalMoment(entry.atUtc, props.language)
}

function eventTextOf(entry: ItemHistoryEntry): string {
  switch (entry.kind) {
    case 'ordered':
      return t('phone.itemHistory.events.ordered')
    case 'handedOut':
      return t('phone.itemHistory.events.handedOut')
    case 'putBack':
      return t('phone.itemHistory.events.putBack')
    case 'cancelled':
      return t('phone.itemHistory.events.cancelled')
    case 'cancellationAcknowledged':
      return t('phone.itemHistory.events.cancellationAcknowledged')
    case 'paid':
      return t('phone.itemHistory.events.paid')
    default:
      return assertNever(entry)
  }
}

function paymentTextOf(entry: ItemHistoryEntry): string | null {
  if (entry.kind !== 'paid' || entry.amountCents === null) {
    return null
  }
  const amount = formatPrice(entry.amountCents, props.language)
  switch (entry.paymentMethod) {
    case 'cash':
      return t('phone.itemHistory.labels.paidInCash', { amount })
    case 'card':
      return t('phone.itemHistory.labels.paidByCard', { amount })
    case 'none':
    case null:
      return amount
    default:
      return assertNever(entry.paymentMethod)
  }
}

function paymentNoticeOf(entry: ItemHistoryEntry): string | null {
  return entry.kind === 'paid' ? entry.paymentNotice : null
}

onMounted(load)
</script>

<template>
  <BaseFullPageDialog class="item-history-dialog" data-test="item-history" :title="item.itemName">
    <p v-if="item.note !== null" class="app-small-text text-medium-emphasis mb-2" data-test="history-note">
      {{ t('common.labels.note', { note: item.note }) }}
    </p>
    <p class="app-figure mb-4" data-test="details-price">{{ priceText }}</p>
    <v-progress-circular v-if="answer === null" class="d-block mx-auto my-6" data-test="history-loading" indeterminate />
    <v-alert v-if="failureKey !== null" data-test="history-failed" type="warning" variant="tonal">
      {{ t(failureKey) }}
    </v-alert>
    <ol v-if="entries.length > 0" class="timeline" data-test="history-timeline">
      <li
        v-for="(entry, position) in entries"
        :key="`${entry.kind}-${entry.atUtc}-${position}`"
        class="timeline-entry"
        data-test="history-entry"
        :data-test-id="entry.kind"
      >
        <span class="entry-dot" />
        <div class="entry-body">
          <div class="entry-time app-small-text text-medium-emphasis" data-test="entry-time">{{ momentOf(entry) }}</div>
          <div class="app-text font-weight-medium" data-test="entry-event">{{ eventTextOf(entry) }}</div>
          <div v-if="entry.by !== null" class="app-small-text text-medium-emphasis" data-test="entry-by">{{ entry.by }}</div>
          <div v-if="paymentTextOf(entry) !== null" class="app-text" data-test="entry-payment">
            {{ paymentTextOf(entry) }}
          </div>
          <div v-if="paymentNoticeOf(entry) !== null" class="app-small-text" data-test="entry-notice">
            {{ t('phone.itemHistory.labels.paymentNotice', { notice: paymentNoticeOf(entry) }) }}
          </div>
        </div>
      </li>
    </ol>
    <template #actions>
      <v-btn
        v-if="answer?.kind === 'unanswered'"
        data-test="history-reload"
        variant="outlined"
        @click="load"
      >
        {{ t('phone.openItems.actions.reload') }}
      </v-btn>
      <v-btn data-test="history-close" color="primary" @click="emit('close')">
        {{ t('common.actions.close') }}
      </v-btn>
    </template>
  </BaseFullPageDialog>
</template>

<style scoped>
.timeline {
  list-style: none;
  padding: 0;
  margin: 0;
}

.timeline-entry {
  display: grid;
  grid-template-columns: 1rem minmax(0, 1fr);
  column-gap: 0.75rem;
  position: relative;
  padding-bottom: 1.25rem;
}

.timeline-entry:last-child {
  padding-bottom: 0;
}

.timeline-entry::before {
  content: '';
  position: absolute;
  top: 0.75rem;
  bottom: 0;
  left: calc(0.5rem - 1px);
  width: 2px;
  background: rgba(var(--v-border-color), var(--v-border-opacity));
}

.timeline-entry:last-child::before {
  display: none;
}

.entry-time {
  font-variant-numeric: tabular-nums;
}

.entry-dot {
  width: 0.75rem;
  height: 0.75rem;
  margin: 0.25rem auto 0;
  border-radius: 50%;
  background: rgb(var(--v-theme-on-surface));
  position: relative;
  z-index: 1;
}

.entry-body {
  min-width: 0;
  overflow-wrap: anywhere;
}
</style>
