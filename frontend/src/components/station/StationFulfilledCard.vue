<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationSlice } from '../../core/apiTypes'
import { deliveryModeKey, isFulfilled } from '../../core/stationBoard'

const props = defineProps<{ slice: StationSlice; isWorking: boolean }>()
const emit = defineEmits<{ putBack: [orderItemId: string] }>()

const { t } = useI18n()

const deliveryText = computed(() => t(deliveryModeKey(props.slice.deliveryMode)))
const doneCounter = computed(() =>
  t('station.doneCounter', {
    fulfilled: props.slice.fulfilledItemCount,
    total: props.slice.itemCount,
  }),
)
</script>

<template>
  <v-card class="station-fulfilled mb-4" variant="outlined">
    <v-card-title class="slice-heading text-subtitle-1">
      {{
        t('station.order', {
          order: slice.globalOrderNumber,
          sequence: slice.stationOrderNumber,
        })
      }}
    </v-card-title>
    <v-card-subtitle class="table-name text-h4">{{ slice.tableName }}</v-card-subtitle>
    <v-card-text>
      <v-chip class="delivery-mode" variant="tonal" size="large">{{ deliveryText }}</v-chip>
      <p class="done-counter text-body-1 mt-2 mb-0">{{ doneCounter }}</p>
      <p v-if="slice.note !== null" class="slice-note text-body-1 mt-1">
        {{ t('station.orderNote', { note: slice.note }) }}
      </p>
      <v-divider class="my-2" />
      <div
        v-for="item in slice.items"
        :key="item.orderItemId"
        class="station-item d-flex align-center ga-3 py-2"
        :class="{ fulfilled: isFulfilled(item) }"
      >
        <v-icon v-if="isFulfilled(item)" class="item-tick" icon="mdi-check" />
        <div class="flex-grow-1">
          <div class="item-name text-body-1">{{ item.itemName }}</div>
          <div v-if="item.note !== null" class="item-note text-body-2">
            {{ t('station.note', { note: item.note }) }}
          </div>
        </div>
        <v-btn
          v-if="isFulfilled(item)"
          class="put-back"
          variant="outlined"
          size="large"
          :disabled="isWorking"
          @click="emit('putBack', item.orderItemId)"
        >
          {{ t('station.putBack') }}
        </v-btn>
      </div>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.station-item.fulfilled .item-name {
  color: rgba(var(--v-theme-on-surface), var(--v-medium-emphasis-opacity));
}

.put-back {
  flex: 0 0 auto;
}
</style>
