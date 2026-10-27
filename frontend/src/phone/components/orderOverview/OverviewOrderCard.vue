<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { TableOrderRecordView } from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { formatFestivalMoment } from '../../../shared/core/moments'
import { isCancelled, isHandedOut } from '../../../shared/core/orderItemStates'
import { notCancelledItemCountIn, producedCountIn } from '../../core/openItems'

const props = defineProps<{ order: TableOrderRecordView; language: AppLanguage }>()

const { t } = useI18n()

function markIconFor(isDone: boolean): string {
  return isDone ? 'mdi-check' : 'mdi-close'
}

function markColourFor(isDone: boolean): string {
  return isDone ? 'success' : 'error'
}

function takenByText(): string {
  return t('common.labels.takenBy', {
    time: formatFestivalMoment(props.order.createdAtUtc, props.language),
    name: props.order.staffMemberName,
  })
}

function doneCounterText(): string {
  return t('common.labels.doneCounter', {
    fulfilled: producedCountIn(props.order),
    total: notCancelledItemCountIn(props.order),
  })
}
</script>

<template>
  <v-card class="overview-order mb-3" data-test="overview-order" :data-test-id="order.globalOrderNumber">
    <v-card-text>
      <div class="d-flex flex-wrap align-baseline ga-2">
        <span class="text-h6" data-test="order-number">
          {{ t('phone.openItems.labels.order', { order: order.globalOrderNumber }) }}
        </span>
        <span class="text-body-2 text-medium-emphasis" data-test="taken-by">{{ takenByText() }}</span>
        <span class="text-body-2 ms-auto" data-test="done-counter">{{ doneCounterText() }}</span>
      </div>
      <div class="overview-lines mt-2">
        <span />
        <span class="column-heading text-body-2" data-test="paid-heading">{{ t('phone.openItems.labels.paid') }}</span>
        <span class="column-heading text-body-2" data-test="handed-out-heading">{{ t('phone.orderOverview.labels.handedOut') }}</span>
        <template v-for="item in order.items" :key="item.orderItemId">
          <div
            class="overview-line-name"
            :class="{ 'cancelled-item': isCancelled(item.state) }"
            data-test="overview-line"
            :data-test-id="item.orderItemId"
          >
            <div class="text-body-1" data-test="line-name">{{ item.itemName }}</div>
            <div v-if="item.note !== null" class="text-body-2 text-medium-emphasis" data-test="line-note">
              {{ t('common.labels.note', { note: item.note }) }}
            </div>
          </div>
          <v-icon
            class="overview-mark"
            data-test="paid-mark"
            :data-test-id="item.orderItemId"
            :color="markColourFor(item.settledAtUtc !== null)"
            :icon="markIconFor(item.settledAtUtc !== null)"
          />
          <v-icon
            class="overview-mark"
            data-test="handed-out-mark"
            :data-test-id="item.orderItemId"
            :color="markColourFor(isHandedOut(item.state))"
            :icon="markIconFor(isHandedOut(item.state))"
          />
        </template>
      </div>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.overview-lines {
  display: grid;
  grid-template-columns: 1fr auto auto;
  align-items: center;
  column-gap: 1rem;
  row-gap: 0.75rem;
}

.column-heading,
.overview-mark {
  justify-self: center;
}

.overview-line-name {
  min-width: 0;
  overflow-wrap: anywhere;
}
</style>
