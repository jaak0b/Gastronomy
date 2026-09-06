<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { SingleItemCard, ProductionAdvance } from '../../core/stationBoard'
import StationItemLine from './StationItemLine.vue'

defineProps<{ card: SingleItemCard; isWorking: boolean }>()
const emit = defineEmits<{ advance: [orderItemIds: string[], status: ProductionAdvance] }>()

const { t } = useI18n()
</script>

<template>
  <v-card class="station-single mb-4" variant="outlined">
    <v-card-title class="slice-heading text-subtitle-1">
      {{
        t('station.order', {
          order: card.globalOrderNumber,
          sequence: card.stationOrderNumber,
        })
      }}
    </v-card-title>
    <v-card-subtitle class="table-name text-h6">{{ card.tableName }}</v-card-subtitle>
    <v-card-text>
      <p v-if="card.note !== null" class="slice-note text-body-1 mb-1">
        {{ t('station.orderNote', { note: card.note }) }}
      </p>
      <StationItemLine
        :item="card.item"
        :is-working="isWorking"
        @advance="(orderItemIds, status) => emit('advance', orderItemIds, status)"
      />
    </v-card-text>
  </v-card>
</template>
