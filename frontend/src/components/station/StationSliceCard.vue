<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationSlice } from '../../core/apiTypes'
import { sliceAdvance, type ProductionAdvance } from '../../core/stationBoard'
import { advanceSliceKey, deliveryModeKey } from '../../core/productionWording'
import StationItemLine from './StationItemLine.vue'

const props = defineProps<{ slice: StationSlice; isWorking: boolean }>()
const emit = defineEmits<{ advance: [orderItemIds: string[], status: ProductionAdvance] }>()

const { t } = useI18n()

const advance = computed(() => sliceAdvance(props.slice))
const deliveryText = computed(() => t(deliveryModeKey(props.slice.deliveryMode)))
</script>

<template>
  <v-card class="station-slice mb-4" variant="outlined">
    <v-card-title class="slice-heading text-h6">
      {{
        t('station.order', {
          order: slice.globalOrderNumber,
          sequence: slice.stationOrderNumber,
        })
      }}
    </v-card-title>
    <v-card-subtitle class="table-name text-h6">{{ slice.tableName }}</v-card-subtitle>
    <v-card-text>
      <p class="delivery-mode text-body-2 text-medium-emphasis">{{ deliveryText }}</p>
      <p v-if="slice.note !== null" class="slice-note text-body-1 mt-1">
        {{ t('station.orderNote', { note: slice.note }) }}
      </p>
      <v-divider class="my-2" />
      <StationItemLine
        v-for="item in slice.items"
        :key="item.orderItemId"
        :item="item"
        :is-working="isWorking"
        @advance="(orderItemIds, status) => emit('advance', orderItemIds, status)"
      />
    </v-card-text>
    <v-card-actions v-if="advance !== null">
      <v-btn
        class="advance-slice"
        color="primary"
        variant="flat"
        block
        size="x-large"
        :disabled="isWorking"
        @click="emit('advance', advance.orderItemIds, advance.status)"
      >
        {{ t(advanceSliceKey(advance.status)) }}
      </v-btn>
    </v-card-actions>
  </v-card>
</template>
