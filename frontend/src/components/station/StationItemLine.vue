<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationSliceItem } from '../../core/apiTypes'
import { nextProductionStatus, type ProductionAdvance } from '../../core/stationBoard'
import { advanceItemKey, productionStatusKey } from '../../core/productionWording'

const props = defineProps<{ item: StationSliceItem; isWorking: boolean }>()
const emit = defineEmits<{ advance: [orderItemIds: string[], status: ProductionAdvance] }>()

const { t } = useI18n()

const advance = computed(() => nextProductionStatus(props.item.productionStatus))
const statusText = computed(() => t(productionStatusKey(props.item.productionStatus)))
</script>

<template>
  <div class="station-item d-flex align-center ga-3 py-2">
    <div class="flex-grow-1">
      <div class="item-name text-h6">{{ item.itemName }}</div>
      <div v-if="item.note !== null" class="item-note text-body-1">
        {{ t('station.note', { note: item.note }) }}
      </div>
      <div class="item-status text-body-2 text-medium-emphasis">{{ statusText }}</div>
    </div>
    <v-btn
      v-if="advance !== null"
      class="advance-item"
      color="primary"
      variant="tonal"
      size="large"
      height="56"
      :disabled="isWorking"
      @click="emit('advance', [item.orderItemId], advance)"
    >
      {{ t(advanceItemKey(advance)) }}
    </v-btn>
  </div>
</template>
