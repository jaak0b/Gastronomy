<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { CatalogItem } from '../../core/apiTypes'
import { candidateStations } from '../../core/routingPreview'

const props = defineProps<{
  item: CatalogItem
  stationNameFor: (stationId: string) => string
}>()
defineEmits<{ choose: [stationId: string] }>()

const { t } = useI18n()
const choices = candidateStations(props.item)
</script>

<template>
  <v-dialog :model-value="true" max-width="480" persistent>
    <v-card class="line-station-sheet">
      <v-card-title>{{ t('line.whereTitle', { item: item.name }) }}</v-card-title>
      <v-card-text>{{ t('line.whereHelp') }}</v-card-text>
      <v-card-actions class="flex-column align-stretch">
        <v-btn
          v-for="stationId in choices"
          :key="stationId"
          class="station-choice mb-2"
          variant="tonal"
          block
          @click="$emit('choose', stationId)"
        >
          {{ stationNameFor(stationId) }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
